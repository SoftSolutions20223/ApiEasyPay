using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ApiEasyPay.Aplication.Services
{
    public class LoginService
    {
        private readonly ConexionSql _conexionSql;
        private readonly ConexionMongo _conexionMongo;

        public LoginService(ConexionSql conexionSql, ConexionMongo conexionMongo)
        {
            _conexionSql = conexionSql;
            _conexionMongo = conexionMongo;

            // Configurar cadena de conexión principal
            _conexionSql.BdPrincipal = ConfigurationOptions.Instance.StrConexBdSql;
        }

        public async Task<SesionStatusDTO> VerificarEstadoSesionAsync(string usuario)
        {
            var comando = new SqlCommand("SELECT * FROM (SELECT Cod, Token, SesionActiva, 'J' as TipoUsuario FROM Jefes WHERE Usuario = @Usuario UNION ALL SELECT Cod, Token, SesionActiva, 'C' as TipoUsuario FROM Cobrador WHERE Usuario = @Usuario) as Users");
            comando.Parameters.AddWithValue("@Usuario", usuario);

            string resultado = _conexionSql.SqlJsonComand(true, comando);

            // Si no hay resultados, el usuario no existe
            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return new SesionStatusDTO { Existe = false, UsuarioExiste = false };

            var jsonObj = JArray.Parse(resultado).FirstOrDefault();
            if (jsonObj == null)
                return new SesionStatusDTO { Existe = false, UsuarioExiste = false };

            return new SesionStatusDTO
            {
                Existe = true,
                UsuarioExiste = true,
                Cod = int.Parse(jsonObj["Cod"].ToString()),
                SesionActiva = bool.Parse(jsonObj["SesionActiva"].ToString()),
                TipoUsuario = jsonObj["TipoUsuario"].ToString(),
                RequiereCodigoRecuperacion = jsonObj["TipoUsuario"].ToString() == "C" && bool.Parse(jsonObj["SesionActiva"].ToString())
            };
        }

        public async Task<SesionDTO> IniciarSesionAsync(string usuario, string contraseña, string codigoRecuperacion = null)
        {
            var comando = new SqlCommand("EXEC PIniciaSesion @Usuario, @Contraseña, @CodRecuperacion");
            comando.Parameters.AddWithValue("@Usuario", usuario);
            comando.Parameters.AddWithValue("@Contraseña", contraseña);
            comando.Parameters.AddWithValue("@CodRecuperacion",
                (object)codigoRecuperacion ?? DBNull.Value);

            string resultado = _conexionSql.SqlJsonComand(true, comando);

            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return null;

            var jsonObj = JToken.Parse(resultado);

            // Verificar si es un mensaje de error
            if (jsonObj["msg"] != null)
                throw new Exception(jsonObj["msg"].ToString());

            // Crear token único
            string token = GenerateToken(usuario);

            // Actualizar estado de sesión en SQL mediante procedimiento almacenado
            var updateCmd = new SqlCommand("EXEC PActualizarEstadoSesion @Usuario, @Token, @TipoUsuario");
            updateCmd.Parameters.AddWithValue("@Usuario", usuario);
            updateCmd.Parameters.AddWithValue("@Token", token);
            updateCmd.Parameters.AddWithValue("@TipoUsuario", jsonObj["Rol"].ToString() == "A" ? "J" : "U");
            _conexionSql.SqlJsonComand(true, updateCmd);

            var sesion = new SesionDTO
            {
                Nombres = jsonObj["Nombres"]?.ToString(),
                Cod = int.Parse(jsonObj["Cod"]?.ToString() ?? "0"),
                Host = jsonObj["Host"]?.ToString(),
                Usu = jsonObj["Usu"]?.ToString(),
                Pw = jsonObj["Pw"]?.ToString(),
                TipoBd = int.Parse(jsonObj["TipoBd"]?.ToString() ?? "0"),
                Bd = int.Parse(jsonObj["Bd"]?.ToString() ?? "0"),
                Rol = jsonObj["Rol"]?.ToString(),
                Token = token
            };

            // Configurar conexión del cliente
            _conexionSql.BdCliente = _conexionSql.CreaCadenaConexServ(
                sesion.Host,
                "SysEasyPayV3",
                sesion.Usu,
                sesion.Pw
            );

            // Guardar sesión en MongoDB
            await _conexionMongo.InsertOrUpdateSessionAsync(JObject.FromObject(sesion));

            return sesion;
        }

        public async Task<bool> CerrarSesionAsync(string token, string tipoUsuario)
        {
            // Primero, eliminar la sesión de MongoDB
            await _conexionMongo.DeleteSessionAsync(token);

            // Luego, actualizar el estado en SQL Server
            var comando = new SqlCommand("EXEC PCerrarSesion @Token, @TipoUsuario");
            comando.Parameters.AddWithValue("@Token", token);
            comando.Parameters.AddWithValue("@TipoUsuario", tipoUsuario);

            string resultado = _conexionSql.SqlJsonComand(true, comando);

            // Verificar resultado
            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return false;

            var jsonObj = JToken.Parse(resultado);
            return !jsonObj["msg"].ToString().Contains("Error") &&
                   !jsonObj["msg"].ToString().Contains("No se encontró");
        }

        public async Task<SesionDTO> ValidateSessionAsync(string token)
        {
            var sessionData = await _conexionMongo.GetSessionByTokenAsync(token);
            if (sessionData == null)
                return null;

            return sessionData.ToObject<SesionDTO>();
        }

        public async Task<bool> LogoutAsync(string token)
        {
            await _conexionMongo.DeleteSessionAsync(token);
            return true;
        }

        public async Task<MemoryStream> GenerarArchivoSincronizacionAsync(int cobradorId)
        {
            var datosCompletos = new JObject();

            // 1. Datos del cobrador (incluye token)
            var cmdCobrador = new SqlCommand("SELECT * FROM Cobrador WHERE Cod = @Cod");
            cmdCobrador.Parameters.AddWithValue("@Cod", cobradorId);
            string datosCobrador = _conexionSql.SqlJsonComand(true, cmdCobrador);

            if (!string.IsNullOrEmpty(datosCobrador) && datosCobrador != "[]")
            {
                datosCompletos["cobrador"] = JArray.Parse(datosCobrador).FirstOrDefault();
            }

            // 2. Datos del jefe
            var cmdJefe = new SqlCommand(@"
        SELECT j.Nombres, j.Apellidos, j.Telefono, j.Documento, j.Direccion, j.Correo, j.Cod, j.NumeroCobradores, j.Domingos
        FROM Cobrador c
        INNER JOIN Jefes j ON c.Jefe = j.Cod
        WHERE c.Cod = @Cod");
            cmdJefe.Parameters.AddWithValue("@Cod", cobradorId);
            string datosJefe = _conexionSql.SqlJsonComand(true, cmdJefe);

            if (!string.IsNullOrEmpty(datosJefe) && datosJefe != "[]")
            {
                datosCompletos["jefe"] = JArray.Parse(datosJefe).FirstOrDefault();
            }

            // 3. Datos operativos
            var tablas = new[] { "Clientes", "Creditos", "Cuotas", "Bolsa", "RegDiarioCuotas", "ValoresBolsa", "Amortizaciones", "ViewCobros" };

            foreach (var tabla in tablas)
            {
                var cmd = new SqlCommand($"SELECT * FROM {tabla} WHERE Cobrador = @Cobrador");
                cmd.Parameters.AddWithValue("@Cobrador", cobradorId);
                string datos = _conexionSql.SqlJsonComand(false, cmd);

                if (!string.IsNullOrEmpty(datos) && datos != "[]")
                {
                    datosCompletos[tabla.ToLower()] = JArray.Parse(datos);
                }
            }

            // Comprimir
            var stream = new MemoryStream();
            using (var gzipStream = new GZipStream(stream, CompressionMode.Compress, true))
            using (var writer = new StreamWriter(gzipStream))
            {
                await writer.WriteAsync(datosCompletos.ToString(Formatting.None));
            }

            stream.Position = 0;
            return stream;
        }


        private string GenerateToken(string username)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                string timestamp = DateTime.UtcNow.Ticks.ToString();
                string dataToHash = username + timestamp + Guid.NewGuid().ToString();
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    builder.Append(hashBytes[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}