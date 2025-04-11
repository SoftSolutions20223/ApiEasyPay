using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Helpers;
using ApiEasyPay.Seguridad.Helpers;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using System.Data;

namespace ApiEasyPay.Aplication.Services
{
    public class SincronizacionService
    {
        private readonly ConexionSql _conexionSql;
        private readonly CustomJsonDeserializer _jsonDeserializer;
        private readonly CustomJsonSerializer _jsonSerializer;

        public SincronizacionService(ConexionSql conexionSql)
        {
            _conexionSql = conexionSql;
            _jsonDeserializer = new CustomJsonDeserializer();
            _jsonSerializer = new CustomJsonSerializer();

            // Configurar cadena de conexión principal
            _conexionSql.BdPrincipal = ConfigurationOptions.Instance.StrConexBdSql;
        }

        /// <summary>
        /// Sincroniza datos individuales con la base de datos
        /// </summary>
        public async Task<(bool success, string message, string data)> SincronizarDatosAsync(SincronizacionRequestDTO request)
        {
            try
            {
                // Validar entrada
                if (string.IsNullOrEmpty(request.Tabla) || request.Datos == null)
                {
                    return (false, "Debe proporcionar el nombre de la tabla y los datos a sincronizar", null);
                }

                // Obtenemos el tipo correspondiente al modelo según la tabla
                Type modelType = GetModelTypeForTable(request.Tabla);
                if (modelType == null)
                {
                    return (false, $"No se encontró un modelo que corresponda a la tabla {request.Tabla}", null);
                }

                // Convertimos el JObject a un tipo específico para validar
                object model = _jsonDeserializer.Deserialize(modelType, request.Datos);

                // Verificamos si hay errores de validación
                if (_jsonDeserializer.Errors.Count > 0)
                {
                    return (false, $"Error de validación en los datos: {_jsonDeserializer.Errors.ToString()}", null);
                }

                // Serializamos de vuelta el objeto (esto aplicará los formatos de fecha, etc.)
                JObject datosSerializados = _jsonSerializer.Serialize(model);

                // Si todo está correcto, llamamos al procedimiento DynamicUpsertJson
                var comando = new SqlCommand("DynamicUpsertJson");
                comando.CommandType = CommandType.StoredProcedure;
                comando.Parameters.AddWithValue("@json", datosSerializados.ToString());
                comando.Parameters.AddWithValue("@tabla", request.Tabla);
                comando.Parameters.AddWithValue("@modoEstricto", request.ModoEstricto);
                comando.Parameters.AddWithValue("@procesarPorLotes", false); // Porque es solo un registro
                comando.Parameters.AddWithValue("@tamanoLote", 1);
                comando.Parameters.AddWithValue("@timeoutSeconds", 60);
                comando.Parameters.AddWithValue("@maxReintentos", 3);
                comando.Parameters.AddWithValue("@registrarLog", true);

                string resultado = _conexionSql.SqlJsonComand(false, comando);

                // Si no hay error, retornar los datos sincronizados
                return (true, "Datos sincronizados correctamente", resultado);
            }
            catch (Exception ex)
            {
                return (false, $"Error al sincronizar datos: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Sincroniza datos masivos con la base de datos
        /// </summary>
        public async Task<(bool success, string message, JArray data)> SincronizarDatosMasivoAsync(SincronizacionMasivaRequestDTO request)
        {
            string datosJson = null;
            try
            {
                // Guardar los datos originales para registro
                datosJson = request.DatosMasivos?.ToString(Newtonsoft.Json.Formatting.None);

                // Validar entrada
                if (string.IsNullOrEmpty(request.Tabla) || request.DatosMasivos == null || request.DatosMasivos.Count == 0)
                {
                    // Registrar sincronización fallida
                    RegistrarSincronizacion(request.Tabla, datosJson, "Error", "No Sincronizado", "Validación fallida: Debe proporcionar el nombre de la tabla y al menos un registro a sincronizar");
                    return (false, "Debe proporcionar el nombre de la tabla y al menos un registro a sincronizar", null);
                }

                // Crear una lista para almacenar los registros eliminados
                var eliminados = new List<JObject>();
                // Crear una lista para almacenar los registros a mantener para el upsert
                var registrosParaUpsert = new JArray();

                // Iterar sobre cada objeto del JArray para identificar los marcados como eliminados
                foreach (JToken token in request.DatosMasivos)
                {
                    if (token is JObject jObject)
                    {
                        // Verificar si existe la propiedad "Eliminado" y si su valor es 1
                        if (jObject["Eliminado"] != null && jObject["Eliminado"].Value<int>() == 1)
                        {
                            // Verificar que tenga Cod y Cobrador para identificar el registro
                            if (jObject["Cod"] != null && jObject["Cobrador"] != null)
                            {
                                // Agregar a la lista de eliminados
                                eliminados.Add(jObject);
                            }
                        }
                        else
                        {
                            // Si no está marcado como eliminado, agregar a la lista para upsert
                            // Primero, eliminar la propiedad "Eliminado" si existe ya que no es parte del modelo
                            if (jObject["Eliminado"] != null)
                            {
                                jObject.Remove("Eliminado");
                            }
                            registrosParaUpsert.Add(jObject);
                        }
                    }
                }

                // Procesar los registros marcados como eliminados
                foreach (var registro in eliminados)
                {
                    var cod = registro["Cod"].Value<string>();
                    var cobrador = registro["Cobrador"].Value<string>();

                    // Verificar si el registro existe en la base de datos
                    var comandoVerificar = new SqlCommand($"SELECT COUNT(1) FROM {request.Tabla} WHERE Cod = {cod} AND Cobrador = {cobrador}");
                    var existeRegistro = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificar.CommandText, false));

                    if (existeRegistro > 0)
                    {
                        // El registro existe, procedemos a eliminarlo
                        var comandoEliminar = new SqlCommand($"DELETE FROM {request.Tabla} WHERE Cod = {cod} AND Cobrador = {cobrador}");
                        string resultado = _conexionSql.SqlQueryGestion(comandoEliminar.CommandText, false);

                        if (resultado != "yes")
                        {
                            // Si hay un error al eliminar, podríamos decidir continuar o abortar
                            // Para este caso, solo registramos el error y continuamos
                            // También podríamos agregar esto a un log o a la respuesta
                            Console.WriteLine($"Error al eliminar registro Cod={cod}, Cobrador={cobrador}: {resultado}");
                        }
                    }
                }

                // Continuar solo si hay registros para el upsert
                if (registrosParaUpsert.Count == 0)
                {
                    RegistrarSincronizacion(request.Tabla, datosJson, "Éxito", "Sincronizado", "Solo eliminaciones");
                    return (true, $"Datos procesados correctamente. Se eliminaron {eliminados.Count} registros. No hay registros para actualizar.", new JArray());
                }

                // Obtenemos el tipo correspondiente al modelo según la tabla
                Type modelType = GetModelTypeForTable(request.Tabla);
                if (modelType == null)
                {
                    RegistrarSincronizacion(request.Tabla, datosJson, "Error", "No Sincronizado", "Modelo no encontrado");
                    return (false, $"No se encontró un modelo que corresponda a la tabla {request.Tabla}", null);
                }

                // Usamos el método DeserializeList para validar la lista completa
                object modelList = _jsonDeserializer.DeserializeList(modelType, registrosParaUpsert);

                // Verificamos si hay errores de validación
                if (_jsonDeserializer.Errors.Count > 0)
                {
                    RegistrarSincronizacion(request.Tabla, datosJson, "Error", "No Sincronizado", "Validación de datos :"+_jsonDeserializer.Errors.ToString() );
                    return (false, $"Error de validación en los datos: {_jsonDeserializer.Errors.ToString()}", null);
                }

                // Obtenemos el tipo genérico IEnumerable<T> para usar SerializeList
                Type listType = typeof(IEnumerable<>).MakeGenericType(modelType);

                // Verificamos si modelList es del tipo correcto para SerializeList
                if (listType.IsAssignableFrom(modelList.GetType()))
                {
                    // Serializamos la lista completa para aplicar formatos
                    JArray datosSerializados = _jsonSerializer.SerializeList((dynamic)modelList);

                    // Si todo está correcto, llamamos al procedimiento DynamicUpsertJson
                    var comando = new SqlCommand("DynamicUpsertJson");
                    var strcomand = datosSerializados.ToString();
                    comando.CommandType = CommandType.StoredProcedure;
                    comando.Parameters.AddWithValue("@json", datosSerializados.ToString());
                    comando.Parameters.AddWithValue("@tabla", request.Tabla);
                    comando.Parameters.AddWithValue("@modoEstricto", request.ModoEstricto);
                    comando.Parameters.AddWithValue("@procesarPorLotes", true);
                    comando.Parameters.AddWithValue("@tamanoLote", request.TamanoLote > 0 ? request.TamanoLote : 100);
                    comando.Parameters.AddWithValue("@timeoutSeconds", request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 300);
                    comando.Parameters.AddWithValue("@maxReintentos", request.MaxReintentos > 0 ? request.MaxReintentos : 3);
                    comando.Parameters.AddWithValue("@registrarLog", true);

                    JArray resultado = _conexionSql.SqlJsonCommandArray(false, comando);

                    // Registrar el resultado de la sincronización
                    if(resultado.ToString().Contains("MensajeError")) {
                        RegistrarSincronizacion(request.Tabla, datosJson, "Error", "No Sincronizado", resultado.ToString());
                    }
                    else
                    {
                        RegistrarSincronizacion(request.Tabla, datosJson, "Completa", "Sincronizado", resultado.ToString());
                    }


                        // Agregar información sobre los registros eliminados a la respuesta
                        var respuesta = new JObject();
                    respuesta["Resultado"] = resultado;
                    respuesta["EliminadosCount"] = eliminados.Count;

                    // Si no hay error, retornar los datos sincronizados
                    return (true, $"Datos sincronizados correctamente. Se eliminaron {eliminados.Count} registros. Total procesados para upsert: {datosSerializados.Count}", resultado);
                }
                else
                {
                    RegistrarSincronizacion(request.Tabla, datosJson, "Error", "No Sincronizado", "Tipo de lista incompatible");
                    return (false, "Error al procesar la lista de objetos", null);
                }
            }
            catch (Exception ex)
            {
                // Registrar la excepción
                RegistrarSincronizacion(request.Tabla, datosJson, "Error","No Sincronizado", "Excepción: " + ex.Message);
                return (false, $"Error al sincronizar datos masivos: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Registra una operación de sincronización en la tabla Sincronizaciones
        /// </summary>
        /// <param name="tabla">Nombre de la tabla sincronizada</param>
        /// <param name="datos">Datos JSON enviados en la sincronización</param>
        /// <param name="respuesta">Resultado de la sincronización (Éxito/Error)</param>
        /// <param name="estado">Estado detallado de la sincronización</param>
        private void RegistrarSincronizacion(string tabla, string datos, string respuesta, string estado,string Mensaje)
        {
            try
            {
                // Limitar el tamaño de los datos a guardar si es demasiado grande
                const int maxLongitudDatos = 8000; // Ajustar según sea necesario
                string datosLimitados = datos;
                if (!string.IsNullOrEmpty(datos) && datos.Length > maxLongitudDatos)
                {
                    datosLimitados = datos.Substring(0, maxLongitudDatos) + "...";
                }

                // Escapar comillas simples en los datos y otros campos
                datosLimitados = datosLimitados?.Replace("'", "''");
                string tablaEscapada = (tabla ?? "Desconocida").Replace("'", "''");
                string respuestaEscapada = respuesta?.Replace("'", "''");
                string estadoEscapado = estado?.Replace("'", "''");

                // Crear el comando SQL concatenando los valores directamente
                string sqlQuery = $@"
            INSERT INTO Sincronizaciones (Tabla, Datos, Respuesta, Sincronizado, Fecha,Mensaje)
            VALUES ('{tablaEscapada}', '{datosLimitados}', '{respuestaEscapada}', '{estadoEscapado}', GETDATE(),'{Mensaje}')";

                // Ejecutar el comando SQL
                _conexionSql.SqlQueryGestion(sqlQuery, false);
            }
            catch (Exception ex)
            {
                // Solo registrar el error, no queremos que un fallo en el registro
                // afecte el flujo principal de la sincronización
                Console.WriteLine($"Error al registrar sincronización: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene el tipo de modelo correspondiente a la tabla
        /// </summary>
        private Type GetModelTypeForTable(string tableName)
        {
            // Mapeamos los nombres de tabla a los tipos de modelo correspondientes
            return tableName.ToLower() switch
            {
                "clientes" => typeof(Domain.Model.Clientes),
                "historialsaldos" => typeof(Domain.Model.HistorialSaldos),
                "movfondos" => typeof(Domain.Model.MovFondos),
                "creditos" => typeof(Domain.Model.Creditos),
                "cuotas" => typeof(Domain.Model.Cuotas),
                "bolsa" => typeof(Domain.Model.Bolsa),
                "regdiariocuotas" => typeof(Domain.Model.RegDiarioCuotas),
                "valoresbolsa" => typeof(Domain.Model.ValoresBolsa),
                "viewcobros" => typeof(Domain.Model.ViewCobros),
                "amortizaciones" => typeof(Domain.Model.Amortizaciones),
                "statuss" => typeof(Domain.Model.Statuss),
                // Añadir más mapeos según sea necesario
                _ => null
            };
        }
    }
}