using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using System.Data;

namespace ApiEasyPay.Aplication.Services
{
    public class SesionInicializadorService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SesionInicializadorService> _logger;

        public SesionInicializadorService(
            IServiceScopeFactory scopeFactory,
            ILogger<SesionInicializadorService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task SincronizarSesionesActivas()
        {
            try
            {
                _logger.LogInformation("Iniciando sincronización de sesiones activas entre SQL Server y MongoDB");

                using (var scope = _scopeFactory.CreateScope())
                {
                    var conexionSql = scope.ServiceProvider.GetRequiredService<ConexionSql>();
                    var conexionMongo = scope.ServiceProvider.GetRequiredService<ConexionMongo>();

                    // Configurar cadena de conexión principal
                    conexionSql.BdPrincipal = ConfigurationOptions.Instance.StrConexBdSql;

                    // Obtener sesiones activas de Jefes
                    await SincronizarSesionesJefes(conexionSql, conexionMongo);

                    // Obtener sesiones activas de Cobradores
                    await SincronizarSesionesCobradores(conexionSql, conexionMongo);

                    // Obtener sesiones activas de Delegados
                    await SincronizarSesionesDelegados(conexionSql, conexionMongo);
                }

                _logger.LogInformation("Sincronización de sesiones completada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la sincronización de sesiones: {Message}", ex.Message);
            }
        }

        private async Task SincronizarSesionesJefes(ConexionSql conexionSql, ConexionMongo conexionMongo)
        {
            var comando = new SqlCommand(@"
            SELECT j.*, 
                   b.Bd AS NameBd, 
                   b.Host AS HostBd, 
                   b.Usu AS UsuBd, 
                   b.Pw AS PwBd,
                   j.TipoCuenta AS TipoBd, 
                   'J' AS Rol
            FROM Jefes j
            LEFT JOIN BasesDatos b ON j.Bd = b.Cod
            WHERE j.SesionActiva = 1 AND j.Token IS NOT NULL");

            DataTable jefes = conexionSql.SqlConsulta(comando.CommandText, true);
            _logger.LogInformation("Encontrados {Count} jefes con sesiones activas", jefes.Rows.Count);

            foreach (DataRow row in jefes.Rows)
            {
                try
                {
                    SesionDTO sesion = new SesionDTO
                    {
                        Cod = Convert.ToInt32(row["Cod"]),
                        Nombres = row["Nombres"].ToString(),
                        Usuario = row["Usuario"].ToString(),
                        Contraseña = row["Contraseña"].ToString(),
                        Token = row["Token"].ToString(),
                        Rol = "J",
                        HostBd = row["HostBd"].ToString(),
                        UsuBd = row["UsuBd"].ToString(),
                        PwBd = row["PwBd"].ToString(),
                        TipoBd = Convert.ToInt32(row["TipoBd"]),
                        NameBd = row["NameBd"].ToString()
                    };

                    // Crear o actualizar la sesión en MongoDB
                    await conexionMongo.InsertOrUpdateSessionAsync(JObject.FromObject(sesion));
                    _logger.LogInformation("Sesión del jefe {Id} sincronizada correctamente", sesion.Cod);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al sincronizar sesión del jefe {Id}: {Message}",
                        row["Cod"], ex.Message);
                }
            }
        }

        private async Task SincronizarSesionesCobradores(ConexionSql conexionSql, ConexionMongo conexionMongo)
        {
            var comando = new SqlCommand(@"
            SELECT c.*, 
                   j.TipoCuenta AS TipoBd,
                   bd.Bd AS NameBd, 
                   bd.Host AS HostBd, 
                   bd.Usu AS UsuBd, 
                   bd.Pw AS PwBd, 
                   'C' AS Rol
            FROM Cobrador c
            INNER JOIN Jefes j ON c.Jefe = j.Cod
            LEFT JOIN BasesDatos bd ON j.Bd = bd.Cod
            WHERE c.SesionActiva = 1 AND c.Token IS NOT NULL");

            DataTable cobradores = conexionSql.SqlConsulta(comando.CommandText, true);
            _logger.LogInformation("Encontrados {Count} cobradores con sesiones activas", cobradores.Rows.Count);

            foreach (DataRow row in cobradores.Rows)
            {
                try
                {
                    SesionDTO sesion = new SesionDTO
                    {
                        Cod = Convert.ToInt32(row["Cod"]),
                        Nombres = row["Nombres"].ToString(),
                        Usuario = row["Usuario"].ToString(),
                        Contraseña = row["Contraseña"].ToString(),
                        Token = row["Token"].ToString(),
                        Rol = "C",
                        HostBd = row["HostBd"].ToString(),
                        UsuBd = row["UsuBd"].ToString(),
                        PwBd = row["PwBd"].ToString(),
                        TipoBd = Convert.ToInt32(row["TipoBd"]),
                        NameBd = row["NameBd"].ToString()
                    };

                    // Crear o actualizar la sesión en MongoDB
                    await conexionMongo.InsertOrUpdateSessionAsync(JObject.FromObject(sesion));
                    _logger.LogInformation("Sesión del cobrador {Id} sincronizada correctamente", sesion.Cod);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al sincronizar sesión del cobrador {Id}: {Message}",
                        row["Cod"], ex.Message);
                }
            }
        }

        private async Task SincronizarSesionesDelegados(ConexionSql conexionSql, ConexionMongo conexionMongo)
        {
            var comando = new SqlCommand(@"
            SELECT d.*, 
                   j.TipoCuenta AS TipoBd,
                   bd.Bd AS NameBd, 
                   bd.Host AS HostBd, 
                   bd.Usu AS UsuBd, 
                   bd.Pw AS PwBd, 
                   'D' AS Rol
            FROM Delegado d
            INNER JOIN Jefes j ON d.Jefe = j.Cod
            LEFT JOIN BasesDatos bd ON j.Bd = bd.Cod
            WHERE d.SesionActiva = 1 AND d.Token IS NOT NULL");

            DataTable delegados = conexionSql.SqlConsulta(comando.CommandText, true);
            _logger.LogInformation("Encontrados {Count} delegados con sesiones activas", delegados.Rows.Count);

            foreach (DataRow row in delegados.Rows)
            {
                try
                {
                    SesionDTO sesion = new SesionDTO
                    {
                        Cod = Convert.ToInt32(row["Cod"]),
                        Nombres = row["Nombres"].ToString(),
                        Usuario = row["Usuario"].ToString(),
                        Contraseña = row["Contraseña"].ToString(),
                        Token = row["Token"].ToString(),
                        Rol = "D",
                        HostBd = row["HostBd"].ToString(),
                        UsuBd = row["UsuBd"].ToString(),
                        PwBd = row["PwBd"].ToString(),
                        TipoBd = Convert.ToInt32(row["TipoBd"]),
                        NameBd = row["NameBd"].ToString()
                    };

                    // Crear o actualizar la sesión en MongoDB
                    await conexionMongo.InsertOrUpdateSessionAsync(JObject.FromObject(sesion));
                    _logger.LogInformation("Sesión del delegado {Id} sincronizada correctamente", sesion.Cod);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al sincronizar sesión del delegado {Id}: {Message}",
                        row["Cod"], ex.Message);
                }
            }
        }
    }
}