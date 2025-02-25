using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
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