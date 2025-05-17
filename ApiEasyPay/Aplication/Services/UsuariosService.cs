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
        private readonly ConexionMongo _conexionMongo;
        private readonly IHttpContextAccessor _httpContextAccessor;



        public UsuariosService(ConexionSql conexionSql, IHttpContextAccessor httpContextAccessor, ConexionMongo conexionMongo)
        {
            _conexionSql = conexionSql;
            _conexionMongo = conexionMongo;
            _httpContextAccessor = httpContextAccessor;

            // Configurar cadena de conexión principal
            _conexionSql.BdPrincipal = ConfigurationOptions.Instance.StrConexBdSql;

            // Inicializar valores desde el contexto HTTP
        }



        private JObject ObtenerSesion()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
                throw new UnauthorizedAccessException("Contexto HTTP no disponible");

            var sesionData = context.Items["SesionData"] as JObject;
            if (sesionData == null)
                throw new UnauthorizedAccessException("Información de sesión no disponible");

            // Si el rol es 'A' (Admin/Jefe), obtener su ID
            if (sesionData["Rol"]?.ToString() != "")
            {
                return sesionData;
            }
            else
            {
                // Si no es jefe, verificar que tenga permisos
                throw new UnauthorizedAccessException("Solo los jefes pueden acceder a esta funcionalidad");
            }
        }

        public async Task<(bool success, string message, JArray data)> ObtenerCobradoresDeDelegado2()
        {
            try
            {
                var sesion = ObtenerSesion();
                if (sesion["Rol"].ToString() != "D")
                {
                    return (false, "El token enviado, no pertenece a una cuenta de Delegado.", null);
                }
                int delegadoId = sesion["Cod"]?.Value<int>() ?? 0;
                // Verificar que el delegado exista
                var comandoDelegado = new SqlCommand("SELECT COUNT(*) FROM Delegado WHERE Cod =" + delegadoId.ToString());
                var existeDelegado = Convert.ToInt32(_conexionSql.TraerDato(comandoDelegado.CommandText, true));

                if (existeDelegado == 0)
                    return (false, "El delegado especificado no existe", null);

                // Obtener los cobradores asignados al delegado
                var comando = new SqlCommand(@"
            SELECT c.* 
            FROM Cobrador c
            INNER JOIN Delegados_Cobradores dc ON c.Cod = dc.Cobrador
            WHERE dc.Delegado = " + delegadoId.ToString() + @"
            FOR JSON PATH");

                JArray resultado = _conexionSql.SqlJsonCommandArray(false, comando);

                return (true, "Cobradores obtenidos correctamente", resultado);
            }
            catch (Exception ex)
            {
                return (false, $"Error al obtener cobradores del delegado: {ex.Message}", null);
            }
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


                try
                {
                    // Verificar si hay un token en el resultado
                    string token = resultado["Token"]?.ToString();

                    if (!string.IsNullOrEmpty(token))
                    {
                        // Buscar la sesión por token
                        var sesionActual = await _conexionMongo.GetSessionByTokenAsync(token);

                        if (sesionActual != null)
                        {
                            // Actualizar los campos relevantes
                            sesionActual["Nombres"] = request.Nombres;
                            sesionActual["Apellidos"] = request.Apellidos;
                            sesionActual["Usuario"] = request.Usuario;
                            sesionActual["Contraseña"] = request.Contraseña;

                            // Actualizar en MongoDB sin alterar el token
                            await _conexionMongo.UpdateSessionAsync(token, sesionActual);

                            // Log opcional
                           // Console.WriteLine($"Sesión actualizada para el usuario {request.Usuario} con token {token}");
                        }
                    }
                }
                catch (Exception ex)
                {

                }
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
                var sesion = ObtenerSesion();
                var _jefeId = sesion["Cod"]?.Value<int>() ?? 0;
                string tabla = tipoUsuario.ToUpper() == "D" ? "Delegado" : "Cobrador";
                SqlCommand comando;

                // Verificar si se solicitan todos los usuarios
                if (codigo == "*")
                {
                    comando = new SqlCommand($"SELECT * FROM {tabla} where jefe={_jefeId.ToString()} For json path");
                }
                else
                {
                    // Intentar convertir el código a número
                    if (!int.TryParse(codigo, out int codigoNum))
                    {
                        return (false, "Código de usuario inválido", null);
                    }

                    comando = new SqlCommand($"SELECT * FROM {tabla} WHERE Cod = {codigoNum.ToString()} for json path");
                }
                string jsonResult = _conexionSql.SqlJsonComand(true, comando);
                JArray resultado = JArray.Parse(jsonResult);

                return (true, "Usuario encontrado", resultado);
            }
            catch (Exception ex)
            {
                return (false, $"Error al obtener usuario: {ex.Message}", null);
            }
        }

        public async Task<(bool success, string message, JObject data)> ObtenerUsuarioByTokenAsync()
        {
            try
            {
                // Obtener la sesión del token actual
                var sesion = ObtenerSesion();

                // Crear un objeto SesionResponseDTO a partir de la sesión
                var sesionResponse = new SesionResponseDTO
                {
                    Cod = sesion["Cod"]?.Value<int>() ?? 0,
                    Nombres = sesion["Nombres"]?.ToString(),
                    Apellidos = sesion["Apellidos"]?.ToString(),
                    Usuario = sesion["Usuario"]?.ToString(),
                    Contraseña = sesion["Contraseña"]?.ToString(),
                    Token = sesion["Token"]?.ToString(),
                    Rol = sesion["Rol"]?.ToString()
                };

                // Convertir el objeto SesionResponseDTO a JObject para devolverlo
                var data = JObject.FromObject(sesionResponse);

                return (true, "Usuario encontrado", data);
            }
            catch (Exception ex)
            {
                return (false, $"Error al obtener usuario: {ex.Message}", null);
            }
        }

        // Añadir a UsuariosService.cs
        public async Task<(bool success, string message, JObject data)> AsignarCobradorADelegado(int delegadoId, int cobradorId)
        {
            try
            {
                // Verificar que el delegado y el cobrador existan
                var comandoDelegado = new SqlCommand("SELECT COUNT(*) FROM Delegado WHERE Cod ="+ delegadoId.ToString());
                var existeDelegado = Convert.ToInt32(_conexionSql.TraerDato(comandoDelegado.CommandText, true));
                var comandoCobrador = new SqlCommand("SELECT COUNT(*) FROM Cobrador WHERE Cod ="+cobradorId.ToString());
                var existeCobrador = Convert.ToInt32(_conexionSql.TraerDato(comandoCobrador.CommandText, true));

                if (existeDelegado == 0)
                    return (false, "El delegado especificado no existe", null);

                if (existeCobrador == 0)
                    return (false, "El cobrador especificado no existe", null);

                // Verificar si ya existe la asignación
                var comandoVerificar = new SqlCommand($"SELECT COUNT(*) FROM Delegados_Cobradores WHERE Delegado = {delegadoId.ToString()} AND Cobrador = {cobradorId.ToString()}");
                var existeAsignacion = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificar.CommandText, false));

                if (existeAsignacion > 0)
                    return (false, "El cobrador ya está asignado a este delegado", null);

                // Insertar la asignación
                var comando = new SqlCommand($"INSERT INTO Delegados_Cobradores (Cod, Delegado, Cobrador) VALUES ((SELECT ISNULL(MAX(Cod),0)+1 FROM Delegados_Cobradores), {delegadoId.ToString()}, {cobradorId.ToString()})");

                string resultado = _conexionSql.SqlQueryGestion(comando.CommandText, false);

                if (resultado != "yes")
                    return (false, "Error al asignar cobrador al delegado: " + resultado, null);

                return (true, "Cobrador asignado correctamente al delegado", _conexionSql.SqlJsonCommandObject(false,
                    new SqlCommand($"SELECT * FROM Delegados_Cobradores WHERE Delegado = {delegadoId.ToString()} AND Cobrador = {cobradorId.ToString()} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER")));
            }
            catch (Exception ex)
            {
                return (false, $"Error al asignar cobrador al delegado: {ex.Message}", null);
            }
        }

        public async Task<(bool success, string message, JArray data)> QuitarCobradorDeDelegado(int delegadoId, int cobradorId)
        {
            try
            {
                // Verificar que la asignación exista
                var comandoVerificar = new SqlCommand($"SELECT COUNT(*) FROM Delegados_Cobradores WHERE Delegado = {delegadoId.ToString()} AND Cobrador = {cobradorId.ToString()}");
                var existeAsignacion = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificar.CommandText, false));

                if (existeAsignacion == 0)
                    return (false, "El cobrador no está asignado a este delegado", null);

                // Eliminar la asignación
                var comando = new SqlCommand($"DELETE FROM Delegados_Cobradores WHERE Delegado = {delegadoId.ToString()} AND Cobrador = {cobradorId.ToString()}");

                string resultado = _conexionSql.SqlQueryGestion(comando.CommandText, false);

                if (resultado != "yes")
                    return (false, "Error al quitar cobrador del delegado: " + resultado, null);

                return (true, "Cobrador quitado correctamente del delegado", new JArray());
            }
            catch (Exception ex)
            {
                return (false, $"Error al quitar cobrador del delegado: {ex.Message}", null);
            }
        }

        public async Task<(bool success, string message, JArray data)> ObtenerCobradoresDeDelegado(int delegadoId)
        {
            try
            {
                // Verificar que el delegado exista
                var comandoDelegado = new SqlCommand("SELECT COUNT(*) FROM Delegado WHERE Cod ="+delegadoId.ToString());
                var existeDelegado = Convert.ToInt32(_conexionSql.TraerDato(comandoDelegado.CommandText, true));

                if (existeDelegado == 0)
                    return (false, "El delegado especificado no existe", null);

                // Obtener los cobradores asignados al delegado
                var comando = new SqlCommand(@"
            SELECT c.* 
            FROM Cobrador c
            INNER JOIN Delegados_Cobradores dc ON c.Cod = dc.Cobrador
            WHERE dc.Delegado = "+delegadoId.ToString()+@"
            FOR JSON PATH");

                JArray resultado = _conexionSql.SqlJsonCommandArray(false, comando);

                return (true, "Cobradores obtenidos correctamente", resultado);
            }
            catch (Exception ex)
            {
                return (false, $"Error al obtener cobradores del delegado: {ex.Message}", null);
            }
        }

        public async Task<(bool success, string message)> EliminarDelegado(int delegadoId)
        {
            try
            {
                // Verificar que el delegado exista
                var comandoDelegado = new SqlCommand("SELECT COUNT(*) FROM Delegado WHERE Cod ="+delegadoId.ToString());
                var existeDelegado = Convert.ToInt32(_conexionSql.TraerDato(comandoDelegado.CommandText, true));

                if (existeDelegado == 0)
                    return (false, "El delegado especificado no existe");

                // Eliminar primero las relaciones en Delegados_Cobradores
                var comandoRelaciones = new SqlCommand("DELETE FROM Delegados_Cobradores WHERE Delegado =" + delegadoId.ToString());
                _conexionSql.SqlQueryGestion(comandoRelaciones.CommandText, false);

                // Ahora eliminar el delegado
                var comando = new SqlCommand("DELETE FROM Delegado WHERE Cod =" + delegadoId.ToString());

                string resultado = _conexionSql.SqlQueryGestion(comando.CommandText, true);

                if (resultado != "yes")
                    return (false, "Error al eliminar delegado: " + resultado);

                resultado = _conexionSql.SqlQueryGestion(comando.CommandText, false);
                return (true, "Delegado eliminado correctamente junto con sus asignaciones");
            }
            catch (Exception ex)
            {
                return (false, $"Error al eliminar delegado: {ex.Message}");
            }
        }

        public async Task<(bool success, string message)> EliminarCobrador(int cobradorId, string adminPassword, string token)
        {
            try
            {
                var sesion = ObtenerSesion();
                // Verificar la contraseña del administrador con el token de sesión
                if ( sesion["Rol"].ToString() != "J" ||
                    sesion["Contraseña"].ToString() != adminPassword)
                {
                    return (false, "Contraseña de administrador incorrecta o permisos insuficientes");
                }

                // Verificar que el cobrador exista
                var comandoCobrador = new SqlCommand("SELECT COUNT(*) FROM Cobrador WHERE Cod ="+cobradorId.ToString());
                var existeCobrador = Convert.ToInt32(_conexionSql.TraerDato(comandoCobrador.CommandText, true));

                if (existeCobrador == 0)
                    return (false, "El cobrador especificado no existe");

                var cmdCobradorUser = new SqlCommand("SELECT Usuario FROM Cobrador WHERE Cod =" + cobradorId.ToString());
                var Usuario = _conexionSql.TraerDato(cmdCobradorUser.CommandText, true).ToString();
                await _conexionMongo.DeleteSessionAsyncByUsuario(Usuario);
                // Iniciar una transacción para eliminar todas las referencias al cobrador
                using (var connection = new SqlConnection(_conexionSql.BdCliente))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Eliminar registros en Delegados_Cobradores
                            var cmdDelegadosCobra = new SqlCommand("DELETE FROM Delegados_Cobradores WHERE Cobrador =" + cobradorId.ToString(), connection, transaction);
                            await cmdDelegadosCobra.ExecuteNonQueryAsync();

                            // Eliminar registros en todas las tablas que tengan campo Cobrador
                            // Usamos este array de tablas basándonos en los modelos encontrados en el código
                            string[] tablasConCobrador = new[] {
                        "Amortizaciones", "Bolsa", "Clientes", "Creditos", "Cuotas",
                        "HistorialSaldos", "RegDiarioCuotas", "ValoresBolsa", "ViewCobros"
                    };

                            foreach (var tabla in tablasConCobrador)
                            {
                                var cmdTabla = new SqlCommand($"DELETE FROM {tabla} WHERE Cobrador = {cobradorId }", connection, transaction);
                                await cmdTabla.ExecuteNonQueryAsync();
                            }

                            // Finalmente eliminar el cobrador
                            var cmdCobrador = new SqlCommand("DELETE FROM SysEasyPayV4_Usuarios.dbo.Cobrador WHERE Cod=" + cobradorId.ToString()+"; DELETE FROM Cobrador WHERE Cod =" + cobradorId.ToString(), connection, transaction);
                            await cmdCobrador.ExecuteNonQueryAsync();

                            transaction.Commit();
                            return (true, "Cobrador eliminado correctamente junto con todos sus registros relacionados");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            return (false, $"Error durante la eliminación del cobrador: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error al eliminar cobrador: {ex.Message}");
            }
        }
    }
}