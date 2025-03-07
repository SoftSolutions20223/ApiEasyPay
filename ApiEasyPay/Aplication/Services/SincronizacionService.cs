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

        public SincronizacionService(ConexionSql conexionSql)
        {
            _conexionSql = conexionSql;
            _jsonDeserializer = new CustomJsonDeserializer();

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

                // Deserializamos los datos para validar el formato y la estructura
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

                // Si todo está correcto, llamamos al procedimiento DynamicUpsertJson
                var comando = new SqlCommand("DynamicUpsertJson");
                comando.CommandType = CommandType.StoredProcedure;
                comando.Parameters.AddWithValue("@json", request.Datos.ToString());
                comando.Parameters.AddWithValue("@tabla", request.Tabla);
                comando.Parameters.AddWithValue("@modoEstricto", request.ModoEstricto);
                comando.Parameters.AddWithValue("@procesarPorLotes", false); // Porque es solo un registro
                comando.Parameters.AddWithValue("@tamanoLote", 1);
                comando.Parameters.AddWithValue("@timeoutSeconds", 60);
                comando.Parameters.AddWithValue("@maxReintentos", 3);
                comando.Parameters.AddWithValue("@registrarLog", true);

                string resultado = _conexionSql.SqlJsonComand(true, comando);

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
        public async Task<(bool success, string message, string data)> SincronizarDatosMasivoAsync(SincronizacionMasivaRequestDTO request)
        {
            try
            {
                // Validar entrada
                if (string.IsNullOrEmpty(request.Tabla) || request.DatosMasivos == null || !request.DatosMasivos.Any())
                {
                    return (false, "Debe proporcionar el nombre de la tabla y al menos un registro a sincronizar", null);
                }

                // Obtenemos el tipo correspondiente al modelo según la tabla
                Type modelType = GetModelTypeForTable(request.Tabla);
                if (modelType == null)
                {
                    return (false, $"No se encontró un modelo que corresponda a la tabla {request.Tabla}", null);
                }

                // Validamos cada elemento del array
                var datosValidados = new JArray();
                foreach (var item in request.DatosMasivos)
                {
                    // Convertimos el JObject a un tipo específico para validar
                    object model = _jsonDeserializer.Deserialize(modelType, item);

                    // Si no hay errores de validación, lo añadimos al array validado
                    if (_jsonDeserializer.Errors.Count == 0)
                    {
                        datosValidados.Add(item);
                    }
                    else
                    {
                        // Opcionalmente, podríamos registrar los errores o añadir alguna lógica de manejo
                        // En este caso continuamos procesando los demás elementos
                        continue;
                    }
                }

                // Si no hay datos válidos, terminamos
                if (datosValidados.Count == 0)
                {
                    return (false, "Ningún registro pasó la validación", null);
                }

                // Preparamos el JSON con todos los registros validados
                string jsonData = datosValidados.ToString();

                // Llamamos al procedimiento DynamicUpsertJson con los datos validados
                var comando = new SqlCommand("DynamicUpsertJson");
                comando.CommandType = CommandType.StoredProcedure;
                comando.Parameters.AddWithValue("@json", jsonData);
                comando.Parameters.AddWithValue("@tabla", request.Tabla);
                comando.Parameters.AddWithValue("@modoEstricto", request.ModoEstricto);
                comando.Parameters.AddWithValue("@procesarPorLotes", true);
                comando.Parameters.AddWithValue("@tamanoLote", request.TamanoLote > 0 ? request.TamanoLote : 100);
                comando.Parameters.AddWithValue("@timeoutSeconds", request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 300);
                comando.Parameters.AddWithValue("@maxReintentos", request.MaxReintentos > 0 ? request.MaxReintentos : 3);
                comando.Parameters.AddWithValue("@registrarLog", true);

                string resultado = _conexionSql.SqlJsonComand(true, comando);

                // Si no hay error, retornar los datos sincronizados
                return (true, $"Datos sincronizados correctamente. Total procesados: {datosValidados.Count}/{request.DatosMasivos.Count}", resultado);
            }
            catch (Exception ex)
            {
                return (false, $"Error al sincronizar datos masivos: {ex.Message}", null);
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
                "cobradores" or "cobrador" => typeof(Domain.Model.Cobradores),
                "delegados" or "delegado" => typeof(Domain.Model.Delegados),
                // Añadir más mapeos según sea necesario
                _ => null
            };
        }
    }
}