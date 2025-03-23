using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;

namespace ApiEasyPay.Aplication.Services
{
    public class BolsasService
    {
        private readonly ConexionSql _conexionSql;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BolsasService(ConexionSql conexionSql, IHttpContextAccessor httpContextAccessor)
        {
            _conexionSql = conexionSql;
            _httpContextAccessor = httpContextAccessor;

            // Configurar cadena de conexión principal
            _conexionSql.BdPrincipal = ConfigurationOptions.Instance.StrConexBdSql;

            // Obtener ID del jefe desde el contexto HTTP
        }
        private int ObtenerId()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
                throw new UnauthorizedAccessException("Contexto HTTP no disponible");

            var sesionData = context.Items["SesionData"] as JObject;
            if (sesionData == null)
                throw new UnauthorizedAccessException("Información de sesión no disponible");

            // Si el rol es 'A' (Admin/Jefe), obtener su ID
            if (sesionData["Rol"]?.ToString() == "J" || sesionData["Rol"]?.ToString() == "D")
            {
                return sesionData["Cod"]?.Value<int>() ?? 0;
            }
            else
            {
                // Si no es jefe, verificar que tenga permisos
                throw new UnauthorizedAccessException("Solo los jefes pueden acceder a esta funcionalidad");
            }
        }

        /// <summary>
        /// Obtiene datos resumidos de las bolsas para los cobradores asignados a un delegado específico
        /// </summary>
        /// <param name="delegadoId">ID del delegado</param>
        /// <param name="fecha">Fecha en formato yyyy-MM-dd</param>
        /// <returns>JObject con el resumen de bolsas para la fecha indicada</returns>
        public JObject ObtenerDatosBolsaPorDelegado(string fecha)
        {
            // Verificamos que el delegado pertenezca al jefe actual (seguridad)
            var delegadoId = ObtenerId();
            var comandoVerificarDelegado = new SqlCommand(
                $"SELECT COUNT(1) FROM Delegado WHERE Cod = {delegadoId}");

            int delegadoValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarDelegado.CommandText, true));

            if (delegadoValido == 0)
            {
                throw new UnauthorizedAccessException("El token proporcionado no pertenece a un delegado");
            }

            // Aseguramos que tengamos una fecha válida
            if (string.IsNullOrEmpty(fecha))
            {
                fecha = DateTime.Now.ToString("yyyy-MM-dd");
            }

            // Consulta para obtener resumen de bolsas por delegado con una sola consulta eficiente
            var comando = new SqlCommand(@"
        SELECT 
            CONVERT(VARCHAR(12), '" + fecha + @"', 103) AS Fecha, 
            COUNT(B.Cod) AS CantidadBolsasActivas,
            SUM(ISNULL(B.SaldoActual, 0)) AS TotalSaldoActual,
            SUM(ISNULL(B.TotalEntregas, 0)) AS TotalEntregas,
            SUM(ISNULL(B.TotalGastos, 0)) AS TotalGastos,
            SUM(ISNULL(B.TotalCobrado, 0)) AS TotalCobrado,
            SUM(ISNULL(B.TotalCobradoCUO, 0)) AS TotalCobradoCuotas,
            SUM(ISNULL(B.TotalCobradoEXT, 0)) AS TotalCobradoExtras,
            SUM(ISNULL(B.TotalCobradoDEU, 0)) AS TotalCobradoDeudas,
            SUM(ISNULL(B.TotalPrestado, 0)) AS TotalPrestado,
            (
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                INNER JOIN Cobrador C ON CR.Cobrador = C.Cod 
                INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
                WHERE CONVERT(DATE, CR.FechaRegistro) = '" + fecha + @"' 
                AND DC.Delegado = " + delegadoId + @"
            ) AS CreditosCreadosHoy,
            (
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                INNER JOIN Cobrador C ON CR.Cobrador = C.Cod 
                INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
                WHERE CONVERT(DATE, CR.FechaFin) = '" + fecha + @"' 
                AND CR.Estado = 'T'
                AND DC.Delegado = " + delegadoId + @"
            ) AS CreditosTerminadosHoy
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        WHERE B.Estado = 'A' 
        AND DC.Delegado = " + delegadoId + @"
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");

            string resultado = _conexionSql.SqlJsonComand(false, comando);
            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return null;

            return JObject.Parse(resultado);
        }

        /// <summary>
        /// Obtiene datos resumidos de bolsa para los cobradores de un delegado específico en un rango de fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>JObject con el resumen de bolsas para el rango de fechas indicado</returns>
        public JObject ObtenerDatosBolsaPorDelegadoRango(string fechaInicio, string fechaFin)
        {
            // Verificamos que el delegado pertenezca al jefe actual (seguridad)
            var delegadoId = ObtenerId();
            var comandoVerificarDelegado = new SqlCommand(
                $"SELECT COUNT(1) FROM Delegado WHERE Cod = {delegadoId}");

            int delegadoValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarDelegado.CommandText, true));

            if (delegadoValido == 0)
            {
                throw new UnauthorizedAccessException("El token proporcionado no pertenece a un delegado");
            }

            // Aseguramos que tengamos fechas válidas
            if (string.IsNullOrEmpty(fechaInicio))
            {
                fechaInicio = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"); // Por defecto, 30 días atrás
            }

            if (string.IsNullOrEmpty(fechaFin))
            {
                fechaFin = DateTime.Now.ToString("yyyy-MM-dd"); // Por defecto, hoy
            }

            // Consulta para obtener resumen de bolsas por delegado con una sola consulta eficiente
            var comando = new SqlCommand(@"
        SELECT 
            CONVERT(VARCHAR(12), '" + fechaInicio + @"', 103) + ' al ' + 
            CONVERT(VARCHAR(12), '" + fechaFin + @"', 103) AS RangoFechas,
            COUNT(DISTINCT B.Cod) AS CantidadBolsasActivas,
            SUM(ISNULL(B.SaldoActual, 0)) AS TotalSaldoActual,
            SUM(ISNULL(B.TotalEntregas, 0)) AS TotalEntregas,
            SUM(ISNULL(B.TotalGastos, 0)) AS TotalGastos,
            SUM(ISNULL(B.TotalCobrado, 0)) AS TotalCobrado,
            SUM(ISNULL(B.TotalCobradoCUO, 0)) AS TotalCobradoCuotas,
            SUM(ISNULL(B.TotalCobradoEXT, 0)) AS TotalCobradoExtras,
            SUM(ISNULL(B.TotalCobradoDEU, 0)) AS TotalCobradoDeudas,
            SUM(ISNULL(B.TotalPrestado, 0)) AS TotalPrestado,
            (
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                INNER JOIN Cobrador C ON CR.Cobrador = C.Cod 
                INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
                WHERE CONVERT(DATE, CR.FechaRegistro) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"' 
                AND DC.Delegado = " + delegadoId + @"
            ) AS CreditosCreados,
            (
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                INNER JOIN Cobrador C ON CR.Cobrador = C.Cod 
                INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
                WHERE CONVERT(DATE, CR.FechaFin) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"' 
                AND CR.Estado = 'T'
                AND DC.Delegado = " + delegadoId + @"
            ) AS CreditosTerminados
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        WHERE 
        DC.Delegado = " + delegadoId + @"
        AND CONVERT(DATE, B.FechaInicio) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");

            string resultado = _conexionSql.SqlJsonComand(false, comando);
            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return null;

            return JObject.Parse(resultado);
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas cerradas para los cobradores asignados a un delegado específico
        /// </summary>
        /// <param name="delegadoId">ID del delegado</param>
        /// <returns>JArray con las bolsas cerradas de los cobradores asignados al delegado</returns>
        public JArray ObtenerBolsasCerradasPorDelegado()
        {
            // Primero verificamos que el delegado pertenezca al jefe actual (seguridad)
            var delegadoId = ObtenerId();
            var comandoVerificarDelegado = new SqlCommand(
                $"SELECT COUNT(1) FROM Delegado WHERE Cod = {delegadoId}");

            int delegadoValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarDelegado.CommandText, true));

            if (delegadoValido == 0)
            {
                throw new UnauthorizedAccessException("El token proporcionado no pertenece a un delegado");
            }

            // Consulta para obtener bolsas cerradas de cobradores asignados al delegado
            var comando = new SqlCommand(@"
        SELECT 
            ISNULL(B.TotalGastos, 0) AS TotalGastos,
            ISNULL(B.TotalEntregas, 0) AS TotalEntregas,
            ISNULL(B.TotalCobradoDEU, 0) AS TotalCobradoDEU,
            ISNULL(B.TotalCobradoCUO, 0) AS TotalCobradoCUO,
            ISNULL(B.TotalCobradoEXT, 0) AS TotalCobradoEXT,
            ISNULL(B.TotalCobrado, 0) AS TotalCobrado,
            ISNULL(B.TotalPrestado, 0) AS TotalUsado,
            CONVERT(VARCHAR(12), B.FechaFin, 103) AS FechaFin,
            C.Cod AS CodCobrador,
            B.Cod AS CodBolsa,
            C.Nombres + ' ' + C.Apellidos AS Nombres,
            C.Documento AS Dni,
            B.SaldoActual AS SaldoActual,
            CONVERT(VARCHAR(12), B.FechaInicio, 103) AS FechaInicio,
            D.Cod AS CodDelegado,
            D.Nombres + ' ' + D.Apellidos AS NombreDelegado
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        INNER JOIN Delegado D ON DC.Delegado = D.Cod
        WHERE B.Estado = 'C' 
          AND DC.Delegado = " + delegadoId + " FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas para los cobradores asignados a un delegado específico
        /// </summary>
        /// <param name="delegadoId">ID del delegado</param>
        /// <returns>JArray con las bolsas abiertas de los cobradores asignados al delegado</returns>
        public JArray ObtenerBolsasAbiertasPorDelegado()
        {
            // Primero verificamos que el delegado pertenezca al jefe actual (seguridad)
            var delegadoId = ObtenerId();
            var comandoVerificarDelegado = new SqlCommand(
                $"SELECT COUNT(1) FROM Delegado WHERE Cod = {delegadoId}");

            int delegadoValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarDelegado.CommandText, true));

            if (delegadoValido == 0)
            {
                throw new UnauthorizedAccessException("El token proporcionado no pertenece a un delegado");
            }

            // Obtenemos la fecha actual en formato adecuado para SQL
            string fechaHoy = DateTime.Now.ToString("yyyy-MM-dd");

            // Consulta para obtener bolsas abiertas de cobradores asignados al delegado
            var comando = new SqlCommand(@"
        SELECT 
            ISNULL(B.TotalGastos, 0) AS TotalGastos,
            ISNULL(B.TotalEntregas, 0) AS TotalEntregas,
            ISNULL(B.TotalCobradoDEU, 0) AS TotalCobradoDEU,
            ISNULL(B.TotalCobradoCUO, 0) AS TotalCobradoCUO,
            ISNULL(B.TotalCobradoEXT, 0) AS TotalCobradoEXT,
            ISNULL(B.TotalCobrado, 0) AS TotalCobrado,
            ISNULL(B.TotalPrestado, 0) AS TotalUsado,
            B.Cobrador AS CodCobrador,
            B.Cod AS CodBolsa,
            C.Nombres + ' ' + C.Apellidos AS Nombres,
            C.Documento AS Dni,
            B.SaldoActual AS SaldoActual,
            CONVERT(VARCHAR(12), B.FechaInicio, 103) AS FechaInicio,
            -- Contar créditos creados hoy para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaRegistro) = '" + fechaHoy + @"'
            ), 0) AS CreditosCreadosHoy,
            -- Contar créditos terminados hoy para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaFin) = '" + fechaHoy + @"'
                AND CR.Estado = 'T'
            ), 0) AS CreditosTerminadosHoy
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        WHERE B.Estado = 'A' 
          AND DC.Delegado = " + delegadoId + " FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas de los cobradores asignados a un delegado específico en un rango de fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>JArray con las bolsas abiertas de los cobradores asignados al delegado en el rango de fechas</returns>
        public JArray ObtenerBolsasPorDelegadoRango(string fechaInicio, string fechaFin)
        {
            // Primero verificamos que el delegado pertenezca al jefe actual (seguridad)
            var delegadoId = ObtenerId();
            var comandoVerificarDelegado = new SqlCommand(
                $"SELECT COUNT(1) FROM Delegado WHERE Cod = {delegadoId}");

            int delegadoValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarDelegado.CommandText, true));

            if (delegadoValido == 0)
            {
                throw new UnauthorizedAccessException("El token proporcionado no pertenece a un delegado");
            }

            // Aseguramos que tengamos fechas válidas
            if (string.IsNullOrEmpty(fechaInicio))
            {
                fechaInicio = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"); // Por defecto, 30 días atrás
            }

            if (string.IsNullOrEmpty(fechaFin))
            {
                fechaFin = DateTime.Now.ToString("yyyy-MM-dd"); // Por defecto, hoy
            }

            // Consulta para obtener bolsas abiertas de cobradores asignados al delegado en rango de fechas
            var comando = new SqlCommand(@"
        SELECT 
            ISNULL(B.TotalGastos, 0) AS TotalGastos,
            ISNULL(B.TotalEntregas, 0) AS TotalEntregas,
            ISNULL(B.TotalCobradoDEU, 0) AS TotalCobradoDEU,
            ISNULL(B.TotalCobradoCUO, 0) AS TotalCobradoCUO,
            ISNULL(B.TotalCobradoEXT, 0) AS TotalCobradoEXT,
            ISNULL(B.TotalCobrado, 0) AS TotalCobrado,
            ISNULL(B.TotalPrestado, 0) AS TotalUsado,
            B.Cobrador AS CodCobrador,
            B.Cod AS CodBolsa,
            C.Nombres + ' ' + C.Apellidos AS Nombres,
            C.Documento AS Dni,
            B.SaldoActual AS SaldoActual,
            CONVERT(VARCHAR(12), B.FechaInicio, 103) AS FechaInicio,
            -- Contar créditos creados en el rango para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaRegistro) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
            ), 0) AS CreditosCreados,
            -- Contar créditos terminados en el rango para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaFin) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
                AND CR.Estado = 'T'
            ), 0) AS CreditosTerminados
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        WHERE 
        DC.Delegado = " + delegadoId + @"
        AND CONVERT(DATE, B.FechaInicio) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
        FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas del jefe actual
        /// </summary>
        public JArray ObtenerBolsasAbiertas()
        {
            var _jefeId = ObtenerId();

            // Obtenemos la fecha actual en formato adecuado para SQL
            string fechaHoy = DateTime.Now.ToString("yyyy-MM-dd");

            var comando = new SqlCommand(@"
        SELECT 
            ISNULL(B.TotalGastos, 0) AS TotalGastos,
            ISNULL(B.TotalEntregas, 0) AS TotalEntregas,
            ISNULL(B.TotalCobradoDEU, 0) AS TotalCobradoDEU,
            ISNULL(B.TotalCobradoCUO, 0) AS TotalCobradoCUO,
            ISNULL(B.TotalCobradoEXT, 0) AS TotalCobradoEXT,
            ISNULL(B.TotalCobrado, 0) AS TotalCobrado,
            ISNULL(B.TotalPrestado, 0) AS TotalUsado,
            B.Cobrador AS CodCobrador,
            B.Cod AS CodBolsa,
            C.Nombres + ' ' + C.Apellidos AS Nombres,
            C.Documento AS Dni,
            B.SaldoActual AS SaldoActual,
            CONVERT(VARCHAR(12), B.FechaInicio, 103) AS FechaInicio,
            -- Contar créditos creados hoy para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaRegistro) = '" + fechaHoy + @"'
            ), 0) AS CreditosCreadosHoy,
            -- Contar créditos terminados hoy para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaFin) = '" + fechaHoy + @"'
                AND CR.Estado = 'T'
            ), 0) AS CreditosTerminadosHoy
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        WHERE B.Estado = 'A' 
          AND C.Jefe = " + _jefeId + " FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas del jefe actual en un rango de fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>JArray con las bolsas abiertas en el rango de fechas especificado</returns>
        public JArray ObtenerBolsasRango(string fechaInicio, string fechaFin)
        {
            var _jefeId = ObtenerId();

            // Aseguramos que tengamos fechas válidas
            if (string.IsNullOrEmpty(fechaInicio))
            {
                fechaInicio = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"); // Por defecto, 30 días atrás
            }

            if (string.IsNullOrEmpty(fechaFin))
            {
                fechaFin = DateTime.Now.ToString("yyyy-MM-dd"); // Por defecto, hoy
            }

            var comando = new SqlCommand(@"
        SELECT 
            ISNULL(B.TotalGastos, 0) AS TotalGastos,
            ISNULL(B.TotalEntregas, 0) AS TotalEntregas,
            ISNULL(B.TotalCobradoDEU, 0) AS TotalCobradoDEU,
            ISNULL(B.TotalCobradoCUO, 0) AS TotalCobradoCUO,
            ISNULL(B.TotalCobradoEXT, 0) AS TotalCobradoEXT,
            ISNULL(B.TotalCobrado, 0) AS TotalCobrado,
            ISNULL(B.TotalPrestado, 0) AS TotalUsado,
            B.Cobrador AS CodCobrador,
            B.Cod AS CodBolsa,
            C.Nombres + ' ' + C.Apellidos AS Nombres,
            C.Documento AS Dni,
            B.SaldoActual AS SaldoActual,
            CONVERT(VARCHAR(12), B.FechaInicio, 103) AS FechaInicio,
            -- Contar créditos creados en el rango para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaRegistro) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
            ), 0) AS CreditosCreados,
            -- Contar créditos terminados en el rango para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaFin) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
                AND CR.Estado = 'T'
            ), 0) AS CreditosTerminados
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        WHERE
        C.Jefe = " + _jefeId + @"
        AND CONVERT(DATE, B.FechaInicio) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
        FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas cerradas del jefe actual
        /// </summary>
        public JArray ObtenerBolsasCerradas()
        {
            var _jefeId = ObtenerId();
            var comando = new SqlCommand(@"
                SELECT 
                    ISNULL(B.TotalGastos, 0) AS TotalGastos,
                    ISNULL(B.TotalEntregas, 0) AS TotalEntregas,
                    ISNULL(B.TotalCobradoDEU, 0) AS TotalCobradoDEU,
                    ISNULL(B.TotalCobradoCUO, 0) AS TotalCobradoCUO,
                    ISNULL(B.TotalCobradoEXT, 0) AS TotalCobradoEXT,
                    ISNULL(B.TotalCobrado, 0) AS TotalCobrado,
                    ISNULL(B.TotalPrestado, 0) AS TotalUsado,
                    CONVERT(VARCHAR(12), B.FechaFin, 103) AS FechaFin,
                    C.Cod AS CodCobrador,
                    B.Cod AS CodBolsa,
                    C.Nombres + ' ' + C.Apellidos AS Nombres,
                    C.Documento AS Dni,
                    B.SaldoActual AS SaldoActual,
                    CONVERT(VARCHAR(12), B.FechaInicio, 103) AS FechaInicio
                FROM Bolsa B 
                INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
                WHERE B.Estado = 'C' AND C.Jefe ="+_jefeId + " for json path");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene datos resumidos de bolsa para la fecha indicada
        /// </summary>
        public JObject ObtenerDatosBolsa(string fecha)
        {
            var _jefeId = ObtenerId();

            // Aseguramos que tengamos una fecha válida
            if (string.IsNullOrEmpty(fecha))
            {
                fecha = DateTime.Now.ToString("yyyy-MM-dd");
            }

            var comando = new SqlCommand(@"
        SELECT 
            CONVERT(VARCHAR(12), '" + fecha + @"', 103) AS Fecha,
            COUNT(B.Cod) AS CantidadBolsasActivas,
            SUM(ISNULL(B.SaldoActual, 0)) AS TotalSaldoActual,
            SUM(ISNULL(B.TotalEntregas, 0)) AS TotalEntregas,
            SUM(ISNULL(B.TotalGastos, 0)) AS TotalGastos,
            SUM(ISNULL(B.TotalCobrado, 0)) AS TotalCobrado,
            SUM(ISNULL(B.TotalCobradoCUO, 0)) AS TotalCobradoCuotas,
            SUM(ISNULL(B.TotalCobradoEXT, 0)) AS TotalCobradoExtras,
            SUM(ISNULL(B.TotalCobradoDEU, 0)) AS TotalCobradoDeudas,
            SUM(ISNULL(B.TotalPrestado, 0)) AS TotalPrestado,
            (SELECT ISNULL(Monto, 0) FROM FondoInversion WHERE Jefe = " + _jefeId + @") AS TotalBolsaC,
            (
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                INNER JOIN Cobrador C ON CR.Cobrador = C.Cod 
                WHERE CONVERT(DATE, CR.FechaRegistro) = '" + fecha + @"' 
                AND C.Jefe = " + _jefeId + @"
            ) AS CreditosCreadosHoy,
            (
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                INNER JOIN Cobrador C ON CR.Cobrador = C.Cod 
                WHERE CONVERT(DATE, CR.FechaFin) = '" + fecha + @"' 
                AND CR.Estado = 'T'
                AND C.Jefe = " + _jefeId + @"
            ) AS CreditosTerminadosHoy
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        WHERE B.Estado = 'A' 
        AND C.Jefe = " + _jefeId + @"
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");

            string resultado = _conexionSql.SqlJsonComand(false, comando);
            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return null;

            return JObject.Parse(resultado);
        }

        /// <summary>
        /// Obtiene datos resumidos de bolsa para un rango de fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>JObject con el resumen de bolsas para el rango de fechas indicado</returns>
        public JObject ObtenerDatosBolsaRango(string fechaInicio, string fechaFin)
        {
            var _jefeId = ObtenerId();

            // Aseguramos que tengamos fechas válidas
            if (string.IsNullOrEmpty(fechaInicio))
            {
                fechaInicio = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"); // Por defecto, 30 días atrás
            }

            if (string.IsNullOrEmpty(fechaFin))
            {
                fechaFin = DateTime.Now.ToString("yyyy-MM-dd"); // Por defecto, hoy
            }

            var comando = new SqlCommand(@"
        SELECT 
            CONVERT(VARCHAR(12), '" + fechaInicio + @"', 103) + ' al ' + 
            CONVERT(VARCHAR(12), '" + fechaFin + @"', 103) AS RangoFechas,
            COUNT(DISTINCT B.Cod) AS CantidadBolsasActivas,
            SUM(ISNULL(B.SaldoActual, 0)) AS TotalSaldoActual,
            SUM(ISNULL(B.TotalEntregas, 0)) AS TotalEntregas,
            SUM(ISNULL(B.TotalGastos, 0)) AS TotalGastos,
            SUM(ISNULL(B.TotalCobrado, 0)) AS TotalCobrado,
            SUM(ISNULL(B.TotalCobradoCUO, 0)) AS TotalCobradoCuotas,
            SUM(ISNULL(B.TotalCobradoEXT, 0)) AS TotalCobradoExtras,
            SUM(ISNULL(B.TotalCobradoDEU, 0)) AS TotalCobradoDeudas,
            SUM(ISNULL(B.TotalPrestado, 0)) AS TotalPrestado,
            (SELECT ISNULL(Monto, 0) FROM FondoInversion WHERE Jefe = " + _jefeId + @") AS TotalBolsaC,
            (
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                INNER JOIN Cobrador C ON CR.Cobrador = C.Cod 
                WHERE CONVERT(DATE, CR.FechaRegistro) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"' 
                AND C.Jefe = " + _jefeId + @"
            ) AS CreditosCreados,
            (
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                INNER JOIN Cobrador C ON CR.Cobrador = C.Cod 
                WHERE CONVERT(DATE, CR.FechaFin) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"' 
                AND CR.Estado = 'T'
                AND C.Jefe = " + _jefeId + @"
            ) AS CreditosTerminados
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        WHERE
        C.Jefe = " + _jefeId + @"
        AND CONVERT(DATE, B.FechaInicio) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");

            string resultado = _conexionSql.SqlJsonComand(false, comando);
            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return null;

            return JObject.Parse(resultado);
        }

        /// <summary>
        /// Obtiene las entregas de una bolsa específica
        /// </summary>
        public JArray ObtenerEntregasBolsa(int codBolsa)
        {
            // Verificar que la bolsa pertenezca a un cobrador del jefe actual
            ValidarBolsaPerteneciente(codBolsa);

            var comando = new SqlCommand(@"
                SELECT 
                    Entregas AS Descripcion,
                    Cod,
                    Valor,
                    CONVERT(VARCHAR(12), Fecha, 103) AS Fecha
                FROM ValoresBolsa 
                WHERE Entregas is Not Null And Entregas <> ''
                  AND Bolsa =" + codBolsa.ToString() + " for json path");



            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene los créditos creados en una bolsa específica
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <returns>JArray con los créditos creados en la bolsa</returns>
        public JArray ObtenerCreditosBolsa(int codBolsa)
        {
            // Verificar que la bolsa pertenezca a un cobrador del jefe actual
            ValidarBolsaPerteneciente(codBolsa);

            var comando = new SqlCommand(@"
        SELECT 
    VB.Credito,
    VB.Cod,
    VB.Valor,
    CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha,
    C.TotalPagar AS TotalPagar,
	C.NumeroDeCuotas AS NumeroDeCuotas,
	C.PorceInteres AS PorceInteres,
    CL.Nombres + ' ' + CL.Apellidos AS NombreCliente,
    CL.Documento AS DocumentoCliente
FROM ValoresBolsa VB
INNER JOIN Creditos C ON VB.Credito = C.Cod
INNER JOIN Clientes CL ON C.Cliente = CL.Cod
WHERE VB.Credito IS NOT NULL 
  AND VB.Credito > 0
          AND VB.Bolsa = " + codBolsa.ToString() + " FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene los créditos creados en una bolsa específica en un rango de fechas
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>JArray con los créditos creados en la bolsa en el rango de fechas</returns>
        public JArray ObtenerCreditosBolsaRango( string fechaInicio, string fechaFin)
        {
            var _jefeId = ObtenerId();
            // Aseguramos que tengamos fechas válidas
            if (string.IsNullOrEmpty(fechaInicio))
            {
                fechaInicio = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"); // Por defecto, 30 días atrás
            }

            if (string.IsNullOrEmpty(fechaFin))
            {
                fechaFin = DateTime.Now.ToString("yyyy-MM-dd"); // Por defecto, hoy
            }

            var comando = new SqlCommand(@"
        SELECT 
            VB.Credito,
            VB.Cod,
            VB.Valor,
            CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha,
            C.TotalPagar AS TotalPagar,
            C.NumeroDeCuotas AS NumeroDeCuotas,
            C.PorceInteres AS PorceInteres,
            CL.Nombres + ' ' + CL.Apellidos AS NombreCliente,
            CL.Documento AS DocumentoCliente
        FROM ValoresBolsa VB
        INNER JOIN Creditos C ON VB.Credito = C.Cod
        INNER JOIN Clientes CL ON C.Cliente = CL.Cod
        INNER JOIN Cobrador CO ON VB.Cobrador = CO.Cod
        WHERE VB.Credito IS NOT NULL 
          AND VB.Credito > 0
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND C.Jefe = " + _jefeId + @" 
        FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene los gastos de una bolsa específica
        /// </summary>
        public JArray ObtenerGastosBolsa(int codBolsa)
        {
            // Verificar que la bolsa pertenezca a un cobrador del jefe actual
            ValidarBolsaPerteneciente(codBolsa);

            var comando = new SqlCommand(@"
                SELECT 
                    Gasto AS Descripcion,
                    Cod,
                    Valor,
                    CONVERT(VARCHAR(12), Fecha, 103) AS Fecha
                FROM ValoresBolsa 
                WHERE Gasto is Not Null And Gasto <> ''
                  AND Bolsa =" + codBolsa.ToString() + " for json path");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene los gastos de una bolsa específica en un rango de fechas
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>JArray con los gastos de la bolsa en el rango de fechas</returns>
        public JArray ObtenerGastosBolsaRango( string fechaInicio, string fechaFin)
        {
            // Obtener el ID del jefe actual desde el contexto
            var _jefeId = ObtenerId();
            // Aseguramos que tengamos fechas válidas
            if (string.IsNullOrEmpty(fechaInicio))
            {
                fechaInicio = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"); // Por defecto, 30 días atrás
            }

            if (string.IsNullOrEmpty(fechaFin))
            {
                fechaFin = DateTime.Now.ToString("yyyy-MM-dd"); // Por defecto, hoy
            } 

            var comando = new SqlCommand(@"
        SELECT 
            VB.Gasto AS Descripcion,
            VB.Cod,
            VB.Valor,
            CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha
       FROM ValoresBolsa VB
        INNER JOIN Bolsa B ON VB.Bolsa = B.Cod
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod
        WHERE VB.Gasto IS NOT NULL 
          AND VB.Gasto <> ''
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND C.Jefe = " + _jefeId + @"
        ORDER BY VB.Fecha DESC
        FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene los créditos creados para los cobradores asignados a un delegado específico en un rango de fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>JArray con los créditos creados en el rango de fechas</returns>
        public JArray ObtenerCreditosPorDelegadoRango(string fechaInicio, string fechaFin)
        {
            // Primero verificamos que el delegado pertenezca al jefe actual (seguridad)
            var delegadoId = ObtenerId();
            var comandoVerificarDelegado = new SqlCommand(
                $"SELECT COUNT(1) FROM Delegado WHERE Cod = {delegadoId}");

            int delegadoValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarDelegado.CommandText, true));

            if (delegadoValido == 0)
            {
                throw new UnauthorizedAccessException("El token proporcionado no pertenece a un delegado");
            }

            // Aseguramos que tengamos fechas válidas
            if (string.IsNullOrEmpty(fechaInicio))
            {
                fechaInicio = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"); // Por defecto, 30 días atrás
            }

            if (string.IsNullOrEmpty(fechaFin))
            {
                fechaFin = DateTime.Now.ToString("yyyy-MM-dd"); // Por defecto, hoy
            }

            var comando = new SqlCommand(@"
        SELECT 
            VB.Credito,
            VB.Cod,
            VB.Valor,
            CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha,
            C.TotalPagar AS TotalPagar,
            C.NumeroDeCuotas AS NumeroDeCuotas,
            C.PorceInteres AS PorceInteres,
            CL.Nombres + ' ' + CL.Apellidos AS NombreCliente,
            CL.Documento AS DocumentoCliente,
            CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador
        FROM ValoresBolsa VB
        INNER JOIN Creditos C ON VB.Credito = C.Cod
        INNER JOIN Clientes CL ON C.Cliente = CL.Cod
        INNER JOIN Cobrador CB ON VB.Cobrador = CB.Cod
        INNER JOIN Delegados_Cobradores DC ON CB.Cod = DC.Cobrador
        WHERE VB.Credito IS NOT NULL 
          AND VB.Credito > 0
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND DC.Delegado = " + delegadoId + @" 
        ORDER BY VB.Fecha DESC
        FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene los gastos de los cobradores asignados a un delegado específico en un rango de fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>JArray con los gastos en el rango de fechas</returns>
        public JArray ObtenerGastosPorDelegadoRango(string fechaInicio, string fechaFin)
        {
            // Primero verificamos que el delegado pertenezca al jefe actual (seguridad)
            var delegadoId = ObtenerId();
            var comandoVerificarDelegado = new SqlCommand(
                $"SELECT COUNT(1) FROM Delegado WHERE Cod = {delegadoId}");

            int delegadoValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarDelegado.CommandText, true));

            if (delegadoValido == 0)
            {
                throw new UnauthorizedAccessException("El token proporcionado no pertenece a un delegado");
            }

            // Aseguramos que tengamos fechas válidas
            if (string.IsNullOrEmpty(fechaInicio))
            {
                fechaInicio = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"); // Por defecto, 30 días atrás
            }

            if (string.IsNullOrEmpty(fechaFin))
            {
                fechaFin = DateTime.Now.ToString("yyyy-MM-dd"); // Por defecto, hoy
            }

            var comando = new SqlCommand(@"
        SELECT 
            VB.Gasto AS Descripcion,
            VB.Cod,
            VB.Valor,
            CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha,
            B.Cod AS CodBolsa,
            CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador
        FROM ValoresBolsa VB
        INNER JOIN Bolsa B ON VB.Bolsa = B.Cod
        INNER JOIN Cobrador CB ON B.Cobrador = CB.Cod
        INNER JOIN Delegados_Cobradores DC ON CB.Cod = DC.Cobrador
        WHERE VB.Gasto IS NOT NULL 
          AND VB.Gasto <> ''
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND DC.Delegado = " + delegadoId + @"
        ORDER BY VB.Fecha DESC
        FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene las entregas de los cobradores asignados a un delegado específico en un rango de fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>JArray con las entregas en el rango de fechas</returns>
        public JArray ObtenerEntregasPorDelegadoRango(string fechaInicio, string fechaFin)
        {
            // Primero verificamos que el delegado pertenezca al jefe actual (seguridad)
            var delegadoId = ObtenerId();
            var comandoVerificarDelegado = new SqlCommand(
                $"SELECT COUNT(1) FROM Delegado WHERE Cod = {delegadoId}");

            int delegadoValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarDelegado.CommandText, true));

            if (delegadoValido == 0)
            {
                throw new UnauthorizedAccessException("El token proporcionado no pertenece a un delegado");
            }

            // Aseguramos que tengamos fechas válidas
            if (string.IsNullOrEmpty(fechaInicio))
            {
                fechaInicio = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"); // Por defecto, 30 días atrás
            }

            if (string.IsNullOrEmpty(fechaFin))
            {
                fechaFin = DateTime.Now.ToString("yyyy-MM-dd"); // Por defecto, hoy
            }

            var comando = new SqlCommand(@"
        SELECT 
            VB.Entregas AS Descripcion,
            VB.Cod,
            VB.Valor,
            CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha,
            B.Cod AS CodBolsa,
            CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador
        FROM ValoresBolsa VB
        INNER JOIN Bolsa B ON VB.Bolsa = B.Cod
        INNER JOIN Cobrador CB ON B.Cobrador = CB.Cod
        INNER JOIN Delegados_Cobradores DC ON CB.Cod = DC.Cobrador
        WHERE VB.Entregas IS NOT NULL 
          AND VB.Entregas <> ''
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND DC.Delegado = " + delegadoId + @"
        ORDER BY VB.Fecha DESC
        FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);

            // Si no hay resultados, devolver un array vacío
            if (string.IsNullOrEmpty(jsonResult) || jsonResult == "[]")
                return new JArray();

            JArray resultado = JArray.Parse(jsonResult);
            return resultado;
        }

        /// <summary>
        /// Obtiene las entregas de todas las bolsas del jefe actual entre dos fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha de inicio en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha de fin en formato yyyy-MM-dd</param>
        /// <returns>Lista de entregas en formato JSON</returns>
        public JArray ObtenerEntregasRango(string fechaInicio, string fechaFin)
        {

            // Aseguramos que tengamos fechas válidas
            if (string.IsNullOrEmpty(fechaInicio))
            {
                fechaInicio = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd"); // Por defecto, 30 días atrás
            }

            if (string.IsNullOrEmpty(fechaFin))
            {
                fechaFin = DateTime.Now.ToString("yyyy-MM-dd"); // Por defecto, hoy
            }
            // Obtener el ID del jefe actual desde el contexto
            var _jefeId = ObtenerId();

            var comando = new SqlCommand(@"
        SELECT 
            VB.Entregas AS Descripcion,
            VB.Cod,
            VB.Valor,
            CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha
        FROM ValoresBolsa VB
        INNER JOIN Bolsa B ON VB.Bolsa = B.Cod
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod
        WHERE VB.Entregas IS NOT NULL 
          AND VB.Entregas <> ''
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND C.Jefe = " + _jefeId + @"
        ORDER BY VB.Fecha DESC
        FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);

            // Si no hay resultados, devolver un array vacío
            if (string.IsNullOrEmpty(jsonResult) || jsonResult == "[]")
                return new JArray();

            JArray resultado = JArray.Parse(jsonResult);
            return resultado;
        }

        /// <summary>
        /// Valida que la bolsa pertenezca a un cobrador del jefe actual
        /// </summary>
        private void ValidarBolsaPerteneciente(int codBolsa)
        {
            var _jefeId = ObtenerId();
            var comando = new SqlCommand(@"
                SELECT COUNT(1) 
                FROM Bolsa B
                INNER JOIN Cobrador C ON B.Cobrador = C.Cod
                WHERE B.Cod = "+codBolsa+" AND C.Jefe ="+_jefeId.ToString() + " for json path");

            int count = Convert.ToInt32(_conexionSql.TraerDato(comando.CommandText, true));

            if (count == 0)
            {
                throw new UnauthorizedAccessException("La bolsa especificada no pertenece a un cobrador de este jefe");
            }
        }
    }
}