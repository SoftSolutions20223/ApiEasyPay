using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
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
            var comando = new SqlCommand("SELECT * FROM (SELECT Cod, Token, SesionActiva, 'J' as TipoUsuario FROM Jefes WHERE Usuario = @Usuario UNION ALL SELECT Cod, Token, SesionActiva, 'C' as TipoUsuario FROM Cobrador WHERE Usuario = @Usuario) as Users FOR JSON PATH");
            comando.Parameters.AddWithValue("@Usuario", usuario);

            string resultado = _conexionSql.SqlJsonComand(true, comando);

            // Si no hay resultados, el usuario no existe
            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return new SesionStatusDTO { Existe = false, UsuarioExiste = false };

            var jsonArray = JArray.Parse(resultado);
            if (jsonArray.Count == 0)
                return new SesionStatusDTO { Existe = false, UsuarioExiste = false };

            var jsonObj = jsonArray.First();

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
            if (resultado.Contains("msg"))
            {
                JObject msgRes = JObject.Parse(resultado);
                JObject ObjetoError = new JObject();
                ObjetoError["MensajeError"] = msgRes["msg"];
                ObjetoError["FuncionOrigen"] = "IniciarSesionAsync";
                ObjetoError["ProcedimientoError"] = comando.CommandText;
                return null;
            }
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
            var outputStream = new MemoryStream();

            using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress, true))
            using (var writer = new StreamWriter(gzipStream))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                jsonWriter.WriteStartObject();

                // 1. Datos del cobrador con SqlDataReader
                using (var conn = new SqlConnection(_conexionSql.BdPrincipal))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand("SELECT * FROM Cobrador WHERE Cod = @Cod", conn))
                    {
                        cmd.Parameters.AddWithValue("@Cod", cobradorId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            jsonWriter.WritePropertyName("cobrador");
                            await SerializarReaderComoObjetoAsync(reader, jsonWriter);
                        }
                    }

                    // 2. Datos del jefe
                    using (var cmd = new SqlCommand(@"
                SELECT j.Nombres, j.Apellidos, j.Telefono, j.Documento, j.Direccion, j.Correo, j.Cod, j.NumeroCobradores, j.Domingos
                FROM Cobrador c INNER JOIN Jefes j ON c.Jefe = j.Cod WHERE c.Cod = @Cod", conn))
                    {
                        cmd.Parameters.AddWithValue("@Cod", cobradorId);
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            jsonWriter.WritePropertyName("jefe");
                            await SerializarReaderComoObjetoAsync(reader, jsonWriter);
                        }
                    }
                }

                // 3. Datos operativos
                using (var conn = new SqlConnection(_conexionSql.BdCliente))
                {
                    await conn.OpenAsync();
                    var tablas = new[] { "Clientes", "Creditos", "Cuotas", "Bolsa", "RegDiarioCuotas", "ValoresBolsa", "Amortizaciones", "ViewCobros" };

                    foreach (var tabla in tablas)
                    {
                        using (var cmd = new SqlCommand($"SELECT * FROM {tabla} WHERE Cobrador = @Cobrador", conn))
                        {
                            cmd.Parameters.AddWithValue("@Cobrador", cobradorId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                jsonWriter.WritePropertyName(tabla.ToLower());
                                await SerializarReaderComoArrayAsync(reader, jsonWriter);
                            }
                        }
                    }
                }

                jsonWriter.WriteEndObject();
            }

            outputStream.Position = 0;
            return outputStream;
        }

        private async Task SerializarReaderComoObjetoAsync(SqlDataReader reader, JsonWriter writer)
        {
            if (await reader.ReadAsync())
            {
                writer.WriteStartObject();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    writer.WritePropertyName(reader.GetName(i));
                    WriteValue(writer, reader[i]);
                }
                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNull();
            }
        }

        private async Task SerializarReaderComoArrayAsync(SqlDataReader reader, JsonWriter writer)
        {
            writer.WriteStartArray();
            while (await reader.ReadAsync())
            {
                writer.WriteStartObject();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    writer.WritePropertyName(reader.GetName(i));
                    WriteValue(writer, reader[i]);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private void WriteValue(JsonWriter writer, object value)
        {
            if (value == null || value == DBNull.Value)
                writer.WriteNull();
            else
                writer.WriteValue(value);
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