using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Helpers;
using ApiEasyPay.Seguridad.Helpers;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;

namespace ApiEasyPay.Aplication.Services
{
    public class UsuariosService
    {
        private readonly ConexionSql _conexionSql;
        private readonly CustomJsonDeserializer _jsonDeserializer;

        public UsuariosService(ConexionSql conexionSql)
        {
            _conexionSql = conexionSql;
            _jsonDeserializer = new CustomJsonDeserializer();

            // Configurar cadena de conexión principal
            _conexionSql.BdPrincipal = ConfigurationOptions.Instance.StrConexBdSql;
        }

        public async Task<(bool success, string message, JObject data)> CrearUsuarioAsync(UsuarioRequestDTO request)
        {
            try
            {
                // Validar entrada
                if (string.IsNullOrEmpty(request.Nombres) || string.IsNullOrEmpty(request.Usuario) ||
                    string.IsNullOrEmpty(request.Contraseña) || request.Jefe <= 0)
                {
                    return (false, "Debe proporcionar los datos obligatorios: Nombres, Usuario, Contraseña y Jefe", null);
                }

                string procedimiento = request.TipoUsuario.ToUpper() == "D"
                    ? "PCreaDelegadoEnAmbasDB"
                    : "PCreaCobradorEnAmbasDB";

                var comando = new SqlCommand(procedimiento);
                comando.CommandType = System.Data.CommandType.StoredProcedure;

                comando.Parameters.AddWithValue("@Nombres", request.Nombres);
                comando.Parameters.AddWithValue("@Apellidos", request.Apellidos ?? "");
                comando.Parameters.AddWithValue("@Telefono", request.Telefono ?? "");
                comando.Parameters.AddWithValue("@Documento", request.Documento ?? "");
                comando.Parameters.AddWithValue("@Direccion", request.Direccion ?? "");
                comando.Parameters.AddWithValue("@Contraseña", request.Contraseña);
                comando.Parameters.AddWithValue("@Usuario", request.Usuario);
                comando.Parameters.AddWithValue("@Estado", request.Estado ?? true);
                comando.Parameters.AddWithValue("@Jefe", request.Jefe);
                comando.Parameters.AddWithValue("@FechaActual", DateTime.Now.Date);

                JObject resultado = _conexionSql.SqlJsonCommandObject(true, comando);

                // Verificar error de conexión o SQL
                if (resultado["MensajeError"] != null)
                    return (false, resultado["MensajeError"].ToString(), null);

                // Si no hay error, retornar los datos creados
                return (true, "Usuario creado correctamente", resultado);
            }
            catch (Exception ex)
            {
                return (false, $"Error al crear usuario: {ex.Message}", null);
            }
        }

        public async Task<(bool success, string message, JObject data)> ModificarUsuarioAsync(UsuarioRequestDTO request)
        {
            try
            {
                // Validar entrada
                if (request.Cod <= 0 || string.IsNullOrEmpty(request.Nombres) ||
                    string.IsNullOrEmpty(request.Usuario) || string.IsNullOrEmpty(request.Contraseña))
                {
                    return (false, "Debe proporcionar los datos obligatorios: Código, Nombres, Usuario y Contraseña", null);
                }

                string procedimiento = request.TipoUsuario.ToUpper() == "D"
                    ? "PModDelegadoEnAmbasDB"
                    : "PModCobradorEnAmbasDB";

                var comando = new SqlCommand(procedimiento);
                comando.CommandType = System.Data.CommandType.StoredProcedure;

                comando.Parameters.AddWithValue("@Cod", request.Cod);
                comando.Parameters.AddWithValue("@Nombres", request.Nombres);
                comando.Parameters.AddWithValue("@Apellidos", request.Apellidos ?? "");
                comando.Parameters.AddWithValue("@Telefono", request.Telefono ?? "");
                comando.Parameters.AddWithValue("@Documento", request.Documento ?? "");
                comando.Parameters.AddWithValue("@Direccion", request.Direccion ?? "");
                comando.Parameters.AddWithValue("@Contraseña", request.Contraseña);
                comando.Parameters.AddWithValue("@Usuario", request.Usuario);
                comando.Parameters.AddWithValue("@Estado", request.Estado ?? true);

                JObject resultado = _conexionSql.SqlJsonCommandObject(true, comando);

                // Verificar error de conexión o SQL
                if (resultado["MensajeError"] != null)
                    return (false, resultado["MensajeError"].ToString(), null);

                // Si no hay error, retornar los datos actualizados
                return (true, "Usuario modificado correctamente", resultado);
            }
            catch (Exception ex)
            {
                return (false, $"Error al modificar usuario: {ex.Message}", null);
            }
        }

        public async Task<(bool success, string message, JArray data)> ObtenerUsuarioAsync(string codigo, string tipoUsuario)
        {
            try
            {
                string tabla = tipoUsuario.ToUpper() == "D" ? "Delegado" : "Cobrador";
                SqlCommand comando;

                // Verificar si se solicitan todos los usuarios
                if (codigo == "*")
                {
                    comando = new SqlCommand($"SELECT * FROM {tabla} For json path");
                    JArray resultado = _conexionSql.SqlJsonCommandArray(true, comando);


                    return (true, "Usuario encontrado", resultado);
                }
                else
                {
                    // Intentar convertir el código a número
                    if (!int.TryParse(codigo, out int codigoNum))
                    {
                        return (false, "Código de usuario inválido", null);
                    }

                    comando = new SqlCommand($"SELECT * FROM {tabla} WHERE Cod = @Codigo for json path");
                    comando.Parameters.AddWithValue("@Codigo", codigoNum);
                    JArray resultado = _conexionSql.SqlJsonCommandArray(true, comando);


                    return (true, "Usuario encontrado", resultado);
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error al obtener usuario: {ex.Message}", null);
            }
        }
    }
}
