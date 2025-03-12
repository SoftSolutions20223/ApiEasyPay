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
            try
            {
                // Validar entrada
                if (string.IsNullOrEmpty(request.Tabla) || request.DatosMasivos == null || request.DatosMasivos.Count == 0)
                {
                    return (false, "Debe proporcionar el nombre de la tabla y al menos un registro a sincronizar", null);
                }

                // Obtenemos el tipo correspondiente al modelo según la tabla
                Type modelType = GetModelTypeForTable(request.Tabla);
                if (modelType == null)
                {
                    return (false, $"No se encontró un modelo que corresponda a la tabla {request.Tabla}", null);
                }

                // Usamos el método DeserializeList para validar la lista completa
                object modelList = _jsonDeserializer.DeserializeList(modelType, request.DatosMasivos);

                // Verificamos si hay errores de validación
                if (_jsonDeserializer.Errors.Count > 0)
                {
                    // Podríamos devolver los errores detallados para que el cliente sepa qué filas tienen problemas
                    return (false, $"Error de validación en los datos: {_jsonDeserializer.Errors.ToString()}", null);
                }

                // Obtenemos el tipo genérico IEnumerable<T> para usar SerializeList
                Type listType = typeof(IEnumerable<>).MakeGenericType(modelType);

                // Verificamos si modelList es del tipo correcto para SerializeList
                if (listType.IsAssignableFrom(modelList.GetType()))
                {
                    // Serializamos la lista completa para aplicar formatos
                    JArray datosSerializados = _jsonSerializer.SerializeList((dynamic)modelList);
                    string json = datosSerializados.ToString();

                    // Si todo está correcto, llamamos al procedimiento DynamicUpsertJson
                    var comando = new SqlCommand("DynamicUpsertJson");
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

                    // Si no hay error, retornar los datos sincronizados
                    return (true, $"Datos sincronizados correctamente. Total procesados: {datosSerializados.Count}", resultado);
                }
                else
                {
                    return (false, "Error al procesar la lista de objetos", null);
                }
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