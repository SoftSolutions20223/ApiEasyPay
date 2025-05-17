using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;

namespace ApiEasyPay.Aplication.Services
{
    public class VersionAppService
    {
        private readonly ConexionSql _conexionSql;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public VersionAppService(ConexionSql conexionSql, IHttpContextAccessor httpContextAccessor)
        {
            _conexionSql = conexionSql;
            _httpContextAccessor = httpContextAccessor;

            // Configurar cadena de conexión principal
            _conexionSql.BdPrincipal = ConfigurationOptions.Instance.StrConexBdSql;
        }

        /// <summary>
        /// Obtiene la versión de la aplicación del usuario actual
        /// </summary>
        /// <returns>Versión de la aplicación en formato JSON</returns>
        public async Task<(bool success, string message, JObject data)> ObtenerVersionAppAsync()
        {
            try
            {
                // Obtener información del usuario del contexto HTTP
                var context = _httpContextAccessor.HttpContext;
                if (context == null)
                    return (false, "Contexto HTTP no disponible", null);

                var sesionData = context.Items["SesionData"] as JObject;
                if (sesionData == null)
                    return (false, "Información de sesión no disponible", null);

                string rol = sesionData["Rol"]?.ToString();
                int usuarioId = sesionData["Cod"]?.Value<int>() ?? 0;

                string tabla;
                switch (rol)
                {
                    case "C":
                        tabla = "Cobrador";
                        break;
                    case "D":
                        tabla = "Delegado";
                        break;
                    case "J":
                        tabla = "Jefes";
                        break;
                    default:
                        return (false, "Rol de usuario no reconocido", null);
                }

                // Consultar la versión de la aplicación
                var comando = new SqlCommand($"SELECT Cod, Usuario, VersionApp FROM {tabla} WHERE Cod = {usuarioId} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");

                string resultado = _conexionSql.SqlJsonComand(true, comando);

                if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                    return (false, "No se encontró información del usuario", null);

                JObject data = JObject.Parse(resultado);
                return (true, "Versión obtenida correctamente", data);
            }
            catch (Exception ex)
            {
                return (false, $"Error al obtener versión: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Actualiza la versión de la aplicación del usuario actual
        /// </summary>
        /// <param name="request">Datos con la nueva versión</param>
        /// <returns>Resultado de la operación</returns>
        public async Task<(bool success, string message, JObject data)> ActualizarVersionAppAsync(VersionAppRequestDTO request)
        {
            try
            {
                // Validar entrada
                if (string.IsNullOrEmpty(request.VersionApp))
                {
                    return (false, "La versión de la aplicación es requerida", null);
                }

                // Obtener información del usuario del contexto HTTP
                var context = _httpContextAccessor.HttpContext;
                if (context == null)
                    return (false, "Contexto HTTP no disponible", null);

                var sesionData = context.Items["SesionData"] as JObject;
                if (sesionData == null)
                    return (false, "Información de sesión no disponible", null);

                string rol = sesionData["Rol"]?.ToString();
                int usuarioId = sesionData["Cod"]?.Value<int>() ?? 0;

                string tabla;
                switch (rol)
                {
                    case "C":
                        tabla = "Cobrador";
                        break;
                    case "D":
                        tabla = "Delegado";
                        break;
                    case "J":
                        tabla = "Jefes";
                        break;
                    default:
                        return (false, "Rol de usuario no reconocido", null);
                }

                // Actualizar la versión de la aplicación
                var comando = new SqlCommand($"UPDATE {tabla} SET VersionApp = '{request.VersionApp}' WHERE Cod = {usuarioId}");

                string resultado = _conexionSql.SqlQueryGestion(comando.CommandText, true);

                if (resultado != "yes")
                    return (false, "Error al actualizar versión: " + resultado, null);

                // Consultar los datos actualizados
                var comandoConsulta = new SqlCommand($"SELECT Cod, Usuario, VersionApp FROM {tabla} WHERE Cod = {usuarioId} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");

                string resultadoConsulta = _conexionSql.SqlJsonComand(true, comandoConsulta);

                if (string.IsNullOrEmpty(resultadoConsulta) || resultadoConsulta == "[]")
                    return (false, "No se encontró información del usuario después de la actualización", null);

                JObject data = JObject.Parse(resultadoConsulta);
                return (true, "Versión actualizada correctamente", data);
            }
            catch (Exception ex)
            {
                return (false, $"Error al actualizar versión: {ex.Message}", null);
            }
        }
    }
}