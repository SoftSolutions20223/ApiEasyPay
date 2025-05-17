using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Domain.Model;
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
        WHERE DC.Delegado = " + delegadoId + @" And CONVERT(DATE,B.FechaInicio) ='" + fecha + @"'
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

            var comando = new SqlCommand(@"
WITH CajasInfo AS (
    SELECT 
        B.Cod, 
        B.Cobrador, 
        B.FechaInicio, 
        B.SaldoActual, 
        B.TotalGastos, 
        B.TotalCobrado, 
        B.TotalCobradoCUO, 
        B.TotalCobradoEXT, 
        B.TotalCobradoDEU, 
        B.TotalPrestado,
        ROW_NUMBER() OVER (PARTITION BY B.Cobrador ORDER BY B.FechaInicio ASC) AS PrimeraFila,
        ROW_NUMBER() OVER (PARTITION BY B.Cobrador ORDER BY B.FechaInicio DESC) AS UltimaFila
    FROM Bolsa B
    INNER JOIN Cobrador C ON B.Cobrador = C.Cod
    INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
    WHERE DC.Delegado = " + delegadoId + @"
      AND CONVERT(DATE, B.FechaInicio) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
),
PrimerApertura AS (
    SELECT 
        A.Cobrador,
        A.Valor AS ValorApertura,
        A.Fecha AS FechaApertura
    FROM (
        SELECT 
            VB.Cobrador,
            VB.Valor,
            VB.Fecha,
            ROW_NUMBER() OVER (PARTITION BY VB.Cobrador ORDER BY VB.Fecha ASC) AS rn
        FROM ValoresBolsa VB
        WHERE VB.Entregas = 'Apertura de Caja'
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
    ) A
    WHERE A.rn = 1
),
EntregasNoApertura AS (
    SELECT 
        VB.Cobrador,
        SUM(VB.Valor) AS TotalEntregasNoApertura
    FROM ValoresBolsa VB
    WHERE VB.Entregas <> 'Apertura de Caja'
      AND VB.Entregas IS NOT NULL 
      AND VB.Entregas <> ''
      AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
    GROUP BY VB.Cobrador
),
CajasAgrupadas AS (
    SELECT 
        Cobrador,
        COUNT(Cod) AS CantidadBolsas,
        SUM(CASE WHEN UltimaFila = 1 THEN SaldoActual ELSE 0 END) AS TotalSaldoActual,
        SUM(TotalGastos) AS TotalGastos,
        SUM(TotalCobrado) AS TotalCobrado,
        SUM(TotalCobradoCUO) AS TotalCobradoCuotas,
        SUM(TotalCobradoEXT) AS TotalCobradoExtras,
        SUM(TotalCobradoDEU) AS TotalCobradoDeudas,
        SUM(TotalPrestado) AS TotalPrestado
    FROM CajasInfo
    GROUP BY Cobrador
),
CreditosCreadosCTE AS (
    SELECT COUNT(CR.Cod) AS CreditosCreados
    FROM Creditos CR
    INNER JOIN Cobrador C ON CR.Cobrador = C.Cod
    INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
    WHERE CONVERT(DATE, CR.FechaRegistro) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
      AND DC.Delegado = " + delegadoId + @"
),
CreditosTerminadosCTE AS (
    SELECT COUNT(CR.Cod) AS CreditosTerminados
    FROM Creditos CR
    INNER JOIN Cobrador C ON CR.Cobrador = C.Cod
    INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
    WHERE CONVERT(DATE, CR.FechaFin) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
      AND CR.Estado = 'T'
      AND DC.Delegado = " + delegadoId + @"
)
SELECT 
    CONVERT(VARCHAR(12), '" + fechaInicio + @"', 103) + ' al ' + CONVERT(VARCHAR(12), '" + fechaFin + @"', 103) AS RangoFechas,
    COUNT(CA.Cobrador) AS CantidadCobradores,
    SUM(CA.CantidadBolsas) AS CantidadBolsas,
    SUM(CA.TotalSaldoActual) AS TotalSaldoActual,
    ISNULL(SUM(PA.ValorApertura), 0) + ISNULL(SUM(ENA.TotalEntregasNoApertura), 0) AS TotalEntregas,
    SUM(CA.TotalGastos) AS TotalGastos,
    SUM(CA.TotalCobrado) AS TotalCobrado,
    SUM(CA.TotalCobradoCuotas) AS TotalCobradoCuotas,
    SUM(CA.TotalCobradoExtras) AS TotalCobradoExtras,
    SUM(CA.TotalCobradoDeudas) AS TotalCobradoDeudas,
    SUM(CA.TotalPrestado) AS TotalPrestado,
    MAX(CC.CreditosCreados) AS CreditosCreados,
    MAX(CT.CreditosTerminados) AS CreditosTerminados
FROM CajasAgrupadas CA
LEFT JOIN PrimerApertura PA ON CA.Cobrador = PA.Cobrador
LEFT JOIN EntregasNoApertura ENA ON CA.Cobrador = ENA.Cobrador
CROSS JOIN CreditosCreadosCTE CC
CROSS JOIN CreditosTerminadosCTE CT
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
            B.FechaFin AS FechaFin,
            C.Cod AS CodCobrador,
            B.Cod AS CodBolsa,
            C.Nombres + ' ' + C.Apellidos AS Nombres,
            C.Documento AS Dni,
            B.SaldoActual AS SaldoActual,
            B.FechaInicio AS FechaInicio,
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
            B.FechaInicio AS FechaInicio,
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
        /// <returns>JArray con las bolsas abiertas en el rango de fechas, agrupadas por cobrador</returns>
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

            var comando = new SqlCommand(@"
    WITH CajasInfo AS (
        SELECT 
            B.Cod, 
            B.Cobrador, 
            B.FechaInicio, 
            B.SaldoActual, 
            B.TotalGastos, 
            B.TotalCobrado, 
            B.TotalCobradoCUO, 
            B.TotalCobradoEXT, 
            B.TotalCobradoDEU, 
            B.TotalPrestado,
            C.Nombres,
            C.Apellidos,
            C.Documento,
            ROW_NUMBER() OVER (PARTITION BY B.Cobrador ORDER BY B.FechaInicio DESC) AS RowNum
        FROM Bolsa B
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        WHERE DC.Delegado = " + delegadoId + @"
          AND CONVERT(DATE, B.FechaInicio) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
    ),
    CajasSumadas AS (
        SELECT 
            Cobrador,
            MAX(Nombres) AS Nombres,
            MAX(Apellidos) AS Apellidos,
            MAX(Documento) AS Documento,
            SUM(TotalGastos) AS TotalGastos,
            SUM(TotalCobrado) AS TotalCobrado,
            SUM(TotalCobradoCUO) AS TotalCobradoCUO,
            SUM(TotalCobradoEXT) AS TotalCobradoEXT,
            SUM(TotalCobradoDEU) AS TotalCobradoDEU,
            SUM(TotalPrestado) AS TotalPrestado,
            COUNT(Cod) AS CantidadBolsas,
            -- Para el SaldoActual tomamos el de la última caja (la más reciente)
            MAX(CASE WHEN RowNum = 1 THEN SaldoActual ELSE 0 END) AS SaldoActual,
            MAX(CASE WHEN RowNum = 1 THEN FechaInicio ELSE NULL END) AS UltimaFechaInicio,
            MAX(CASE WHEN RowNum = 1 THEN Cod ELSE NULL END) AS UltimaCaja
        FROM CajasInfo
        GROUP BY Cobrador
    ),
    PrimerApertura AS (
        SELECT 
            A.Cobrador,
            A.Valor AS ValorApertura,
            A.Fecha AS FechaApertura
        FROM (
            SELECT 
                VB.Cobrador,
                VB.Valor,
                VB.Fecha,
                ROW_NUMBER() OVER (PARTITION BY VB.Cobrador ORDER BY VB.Fecha ASC) AS rn
            FROM ValoresBolsa VB
            INNER JOIN Cobrador C ON VB.Cobrador = C.Cod
            INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
            WHERE VB.Entregas = 'Apertura de Caja'
              AND DC.Delegado = " + delegadoId + @"
              AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
        ) A
        WHERE A.rn = 1
    ),
    EntregasNoApertura AS (
        SELECT 
            VB.Cobrador,
            SUM(VB.Valor) AS TotalEntregasNoApertura
        FROM ValoresBolsa VB
        INNER JOIN Cobrador C ON VB.Cobrador = C.Cod
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        WHERE VB.Entregas <> 'Apertura de Caja'
          AND VB.Entregas IS NOT NULL 
          AND VB.Entregas <> ''
          AND DC.Delegado = " + delegadoId + @"
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
        GROUP BY VB.Cobrador
    ),
    CreditosPorCobrador AS (
        SELECT 
            CR.Cobrador,
            COUNT(CASE WHEN CONVERT(DATE, CR.FechaRegistro) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"' THEN 1 END) AS CreditosCreados,
            COUNT(CASE WHEN CONVERT(DATE, CR.FechaFin) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"' AND CR.Estado = 'T' THEN 1 END) AS CreditosTerminados
        FROM Creditos CR
        INNER JOIN Cobrador C ON CR.Cobrador = C.Cod
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        WHERE DC.Delegado = " + delegadoId + @"
          AND (CONVERT(DATE, CR.FechaRegistro) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
               OR (CONVERT(DATE, CR.FechaFin) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"' AND CR.Estado = 'T'))
        GROUP BY CR.Cobrador
    ),
    DelegadoInfo AS (
        SELECT
            D.Nombres + ' ' + D.Apellidos AS NombreDelegado,
            D.Documento AS DniDelegado
        FROM Delegado D
        WHERE D.Cod = " + delegadoId + @"
    )
    SELECT 
        CS.Cobrador AS CodCobrador,
        CS.UltimaCaja AS CodBolsa,
        CS.Nombres + ' ' + CS.Apellidos AS Nombres,
        CS.Documento AS Dni,
        CS.SaldoActual,
        CONVERT(VARCHAR(12), CS.UltimaFechaInicio, 103) AS FechaInicio,
        ISNULL(PA.ValorApertura, 0) AS ValorApertura,
        CONVERT(VARCHAR(12), PA.FechaApertura, 103) AS FechaApertura,
        ISNULL(ENA.TotalEntregasNoApertura, 0) AS EntregasAdicionales,
        ISNULL(PA.ValorApertura, 0) + ISNULL(ENA.TotalEntregasNoApertura, 0) AS TotalEntregas,
        ISNULL(CS.TotalGastos, 0) AS TotalGastos,
        ISNULL(CS.TotalCobradoDEU, 0) AS TotalCobradoDEU,
        ISNULL(CS.TotalCobradoCUO, 0) AS TotalCobradoCUO,
        ISNULL(CS.TotalCobradoEXT, 0) AS TotalCobradoEXT,
        ISNULL(CS.TotalCobrado, 0) AS TotalCobrado,
        ISNULL(CS.TotalPrestado, 0) AS TotalUsado,
        ISNULL(CPC.CreditosCreados, 0) AS CreditosCreados,
        ISNULL(CPC.CreditosTerminados, 0) AS CreditosTerminados,
        CS.CantidadBolsas,
        " + delegadoId + @" AS CodDelegado,
        (SELECT NombreDelegado FROM DelegadoInfo) AS NombreDelegado,
        (SELECT DniDelegado FROM DelegadoInfo) AS DniDelegado,
        '" + fechaInicio + @"' AS FechaInicio_Rango,
        '" + fechaFin + @"' AS FechaFin_Rango
    FROM CajasSumadas CS
    LEFT JOIN PrimerApertura PA ON CS.Cobrador = PA.Cobrador
    LEFT JOIN EntregasNoApertura ENA ON CS.Cobrador = ENA.Cobrador
    LEFT JOIN CreditosPorCobrador CPC ON CS.Cobrador = CPC.Cobrador
    ORDER BY CS.Nombres
    FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas del jefe actual
        /// </summary>
        public JArray ObtenerBolsasxFecha(string Fecha)
        {
            var _jefeId = ObtenerId();

            // Aseguramos que tengamos una fecha válida
            if (string.IsNullOrEmpty(Fecha))
            {
                Fecha = DateTime.Now.ToString("yyyy-MM-dd");
            }

            var comando = new SqlCommand(@"
        SELECT 
            ISNULL(B.TotalGastos, 0) AS TotalGastos,
            ISNULL(B.TotalEntregas, 0) AS TotalEntregas,
            ISNULL(B.TotalCobradoDEU, 0) AS TotalCobradoDEU,
            ISNULL(B.TotalCobradoCUO, 0) AS TotalCobradoCUO,
            ISNULL(B.TotalCobradoEXT, 0) AS TotalCobradoEXT,
            ISNULL(B.TotalCobrado, 0) AS TotalCobrado,
            ISNULL(B.TotalPrestado, 0) AS TotalPrestado,
            B.Cobrador AS CodCobrador,
            B.Cod AS CodBolsa,
            B.Estado,
            C.Nombres + ' ' + C.Apellidos AS Nombres,
            C.Documento AS Dni,
            B.SaldoActual AS SaldoActual,
            B.FechaInicio AS FechaInicio,
            -- Contar créditos creados hoy para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaRegistro) = '" + Fecha + @"'
            ), 0) AS CreditosCreadosHoy,
            -- Contar créditos terminados hoy para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaFin) = '" + Fecha + @"'
                AND CR.Estado = 'T'
            ), 0) AS CreditosTerminadosHoy
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        WHERE CONVERT(DATE, B.FechaInicio) ='" + Fecha + @"' And C.Jefe = " + _jefeId + " FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }


        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas para un delegado específico
        /// </summary>
        public JArray ObtenerBolsasPorDelegadoxFecha(string Fecha)
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
            if (string.IsNullOrEmpty(Fecha))
            {
                Fecha = DateTime.Now.ToString("yyyy-MM-dd");
            }

            var comando = new SqlCommand(@"
        SELECT 
            ISNULL(B.TotalGastos, 0) AS TotalGastos,
            ISNULL(B.TotalEntregas, 0) AS TotalEntregas,
            ISNULL(B.TotalCobradoDEU, 0) AS TotalCobradoDEU,
            ISNULL(B.TotalCobradoCUO, 0) AS TotalCobradoCUO,
            ISNULL(B.TotalCobradoEXT, 0) AS TotalCobradoEXT,
            ISNULL(B.TotalCobrado, 0) AS TotalCobrado,
            ISNULL(B.TotalPrestado, 0) AS TotalPrestado,
            B.Cobrador AS CodCobrador,
            B.Cod AS CodBolsa,
            B.Estado,
            C.Nombres + ' ' + C.Apellidos AS Nombres,
            C.Documento AS Dni,
            B.SaldoActual AS SaldoActual,
            B.FechaInicio AS FechaInicio,
            -- Contar créditos creados hoy para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaRegistro) = '" + Fecha + @"'
            ), 0) AS CreditosCreadosHoy,
            -- Contar créditos terminados hoy para este cobrador
            ISNULL((
                SELECT COUNT(CR.Cod) 
                FROM Creditos CR 
                WHERE CR.Cobrador = B.Cobrador 
                AND CONVERT(DATE, CR.FechaFin) = '" + Fecha + @"'
                AND CR.Estado = 'T'
            ), 0) AS CreditosTerminadosHoy
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        WHERE CONVERT(DATE,B.FechaInicio) = '" + Fecha + @"' 
        AND DC.Delegado = " + delegadoId + " FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);
            return resultado;
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas del jefe actual en un rango de fechas, detallado por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>JArray con las bolsas abiertas en el rango de fechas, agrupadas por cobrador</returns>
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
    WITH CajasInfo AS (
        SELECT 
            B.Cod, 
            B.Cobrador, 
            B.FechaInicio, 
            B.SaldoActual, 
            B.TotalGastos, 
            B.TotalCobrado, 
            B.TotalCobradoCUO, 
            B.TotalCobradoEXT, 
            B.TotalCobradoDEU, 
            B.TotalPrestado,
            C.Nombres,
            C.Apellidos,
            C.Documento,
            ROW_NUMBER() OVER (PARTITION BY B.Cobrador ORDER BY B.FechaInicio DESC) AS RowNum
        FROM Bolsa B
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod
        WHERE C.Jefe = " + _jefeId + @"
          AND CONVERT(DATE, B.FechaInicio) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
    ),
    CajasSumadas AS (
        SELECT 
            Cobrador,
            MAX(Nombres) AS Nombres,
            MAX(Apellidos) AS Apellidos,
            MAX(Documento) AS Documento,
            SUM(TotalGastos) AS TotalGastos,
            SUM(TotalCobrado) AS TotalCobrado,
            SUM(TotalCobradoCUO) AS TotalCobradoCUO,
            SUM(TotalCobradoEXT) AS TotalCobradoEXT,
            SUM(TotalCobradoDEU) AS TotalCobradoDEU,
            SUM(TotalPrestado) AS TotalPrestado,
            COUNT(Cod) AS CantidadBolsas,
            -- Para el SaldoActual tomamos el de la última caja (la más reciente)
            MAX(CASE WHEN RowNum = 1 THEN SaldoActual ELSE 0 END) AS SaldoActual,
            MAX(CASE WHEN RowNum = 1 THEN FechaInicio ELSE NULL END) AS UltimaFechaInicio,
            MAX(CASE WHEN RowNum = 1 THEN Cod ELSE NULL END) AS UltimaCaja
        FROM CajasInfo
        GROUP BY Cobrador
    ),
    PrimerApertura AS (
        SELECT 
            A.Cobrador,
            A.Valor AS ValorApertura,
            A.Fecha AS FechaApertura
        FROM (
            SELECT 
                VB.Cobrador,
                VB.Valor,
                VB.Fecha,
                ROW_NUMBER() OVER (PARTITION BY VB.Cobrador ORDER BY VB.Fecha ASC) AS rn
            FROM ValoresBolsa VB
            INNER JOIN Cobrador C ON VB.Cobrador = C.Cod
            WHERE VB.Entregas = 'Apertura de Caja'
              AND C.Jefe = " + _jefeId + @"
              AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
        ) A
        WHERE A.rn = 1
    ),
    EntregasNoApertura AS (
        SELECT 
            VB.Cobrador,
            SUM(VB.Valor) AS TotalEntregasNoApertura
        FROM ValoresBolsa VB
        INNER JOIN Cobrador C ON VB.Cobrador = C.Cod
        WHERE VB.Entregas <> 'Apertura de Caja'
          AND VB.Entregas IS NOT NULL 
          AND VB.Entregas <> ''
          AND C.Jefe = " + _jefeId + @"
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
        GROUP BY VB.Cobrador
    ),
    CreditosPorCobrador AS (
        SELECT 
            CR.Cobrador,
            COUNT(CASE WHEN CONVERT(DATE, CR.FechaRegistro) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"' THEN 1 END) AS CreditosCreados,
            COUNT(CASE WHEN CONVERT(DATE, CR.FechaFin) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"' AND CR.Estado = 'T' THEN 1 END) AS CreditosTerminados
        FROM Creditos CR
        INNER JOIN Cobrador C ON CR.Cobrador = C.Cod
        WHERE C.Jefe = " + _jefeId + @"
          AND (CONVERT(DATE, CR.FechaRegistro) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
               OR (CONVERT(DATE, CR.FechaFin) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"' AND CR.Estado = 'T'))
        GROUP BY CR.Cobrador
    )
    SELECT 
        CS.Cobrador AS CodCobrador,
        CS.UltimaCaja AS CodBolsa,
        CS.Nombres + ' ' + CS.Apellidos AS Nombres,
        CS.Documento AS Dni,
        CS.SaldoActual,
        CONVERT(VARCHAR(12), CS.UltimaFechaInicio, 103) AS FechaInicio,
        ISNULL(PA.ValorApertura, 0) AS ValorApertura,
        CONVERT(VARCHAR(12), PA.FechaApertura, 103) AS FechaApertura,
        ISNULL(ENA.TotalEntregasNoApertura, 0) AS EntregasAdicionales,
        ISNULL(PA.ValorApertura, 0) + ISNULL(ENA.TotalEntregasNoApertura, 0) AS TotalEntregas,
        ISNULL(CS.TotalGastos, 0) AS TotalGastos,
        ISNULL(CS.TotalCobradoDEU, 0) AS TotalCobradoDEU,
        ISNULL(CS.TotalCobradoCUO, 0) AS TotalCobradoCUO,
        ISNULL(CS.TotalCobradoEXT, 0) AS TotalCobradoEXT,
        ISNULL(CS.TotalCobrado, 0) AS TotalCobrado,
        ISNULL(CS.TotalPrestado, 0) AS TotalUsado,
        ISNULL(CPC.CreditosCreados, 0) AS CreditosCreados,
        ISNULL(CPC.CreditosTerminados, 0) AS CreditosTerminados,
        CS.CantidadBolsas,
        '" + fechaInicio + @"' AS FechaInicio_Rango,
        '" + fechaFin + @"' AS FechaFin_Rango
    FROM CajasSumadas CS
    LEFT JOIN PrimerApertura PA ON CS.Cobrador = PA.Cobrador
    LEFT JOIN EntregasNoApertura ENA ON CS.Cobrador = ENA.Cobrador
    LEFT JOIN CreditosPorCobrador CPC ON CS.Cobrador = CPC.Cobrador
    ORDER BY CS.Nombres
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
                    B.FechaFin AS FechaFin,
                    C.Cod AS CodCobrador,
                    B.Cod AS CodBolsa,
                    C.Nombres + ' ' + C.Apellidos AS Nombres,
                    C.Documento AS Dni,
                    B.SaldoActual AS SaldoActual,
                    B.FechaInicio AS FechaInicio
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
        public JObject ObtenerDatosBolsaByFecha(string fecha)
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
            COUNT(B.Cod) AS CantidadBolsas,
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
        WHERE C.Jefe = " + _jefeId + @" And CONVERT(DATE, B.FechaInicio) ='" + fecha + @"'
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");

            string resultado = _conexionSql.SqlJsonComand(false, comando);
            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return null;

            return JObject.Parse(resultado);
        }

        /// <summary>
        /// Obtiene datos resumidos de bolsa para la fecha indicada por delegado
        /// </summary>
        public JObject ObtenerDatosBolsaPorDelegadoByFecha(string fecha)
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

            var comando = new SqlCommand(@"
        SELECT 
            CONVERT(VARCHAR(12), '" + fecha + @"', 103) AS Fecha, 
            COUNT(B.Cod) AS CantidadBolsas,
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
        WHERE DC.Delegado = " + delegadoId + @" AND B.FechaInicio = '" + fecha + @"'
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
        WITH CajasInfo AS (
    SELECT 
        B.Cod, 
        B.Cobrador, 
        B.FechaInicio, 
        B.SaldoActual, 
        B.TotalGastos, 
        B.TotalCobrado, 
        B.TotalCobradoCUO, 
        B.TotalCobradoEXT, 
        B.TotalCobradoDEU, 
        B.TotalPrestado,
        ROW_NUMBER() OVER (PARTITION BY B.Cobrador ORDER BY B.FechaInicio ASC) AS PrimeraFila,
        ROW_NUMBER() OVER (PARTITION BY B.Cobrador ORDER BY B.FechaInicio DESC) AS UltimaFila
    FROM Bolsa B
    INNER JOIN Cobrador C ON B.Cobrador = C.Cod
    WHERE C.Jefe = " + _jefeId + @"
      AND CONVERT(DATE, B.FechaInicio) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
),
PrimerApertura AS (
    SELECT 
        A.Cobrador,
        A.Valor AS ValorApertura,
        A.Fecha AS FechaApertura
    FROM (
        SELECT 
            VB.Cobrador,
            VB.Valor,
            VB.Fecha,
            ROW_NUMBER() OVER (PARTITION BY VB.Cobrador ORDER BY VB.Fecha ASC) AS rn
        FROM ValoresBolsa VB
        INNER JOIN Cobrador C ON VB.Cobrador = C.Cod
        WHERE VB.Entregas = 'Apertura de Caja'
          AND C.Jefe = " + _jefeId + @"
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
    ) A
    WHERE A.rn = 1
),
EntregasNoApertura AS (
    SELECT 
        VB.Cobrador,
        SUM(VB.Valor) AS TotalEntregasNoApertura
    FROM ValoresBolsa VB
    INNER JOIN Cobrador C ON VB.Cobrador = C.Cod
    WHERE VB.Entregas <> 'Apertura de Caja'
      AND VB.Entregas IS NOT NULL 
      AND VB.Entregas <> ''
      AND C.Jefe = " + _jefeId + @"
      AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
    GROUP BY VB.Cobrador
),
CajasAgrupadas AS (
    SELECT 
        Cobrador,
        COUNT(Cod) AS CantidadBolsas,
        SUM(CASE WHEN UltimaFila = 1 THEN SaldoActual ELSE 0 END) AS TotalSaldoActual,
        SUM(TotalGastos) AS TotalGastos,
        SUM(TotalCobrado) AS TotalCobrado,
        SUM(TotalCobradoCUO) AS TotalCobradoCuotas,
        SUM(TotalCobradoEXT) AS TotalCobradoExtras,
        SUM(TotalCobradoDEU) AS TotalCobradoDeudas,
        SUM(TotalPrestado) AS TotalPrestado
    FROM CajasInfo
    GROUP BY Cobrador
),
CreditosCreadosCTE AS (
    SELECT COUNT(CR.Cod) AS CreditosCreados
    FROM Creditos CR
    INNER JOIN Cobrador C ON CR.Cobrador = C.Cod
    WHERE CONVERT(DATE, CR.FechaRegistro) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
      AND C.Jefe = " + _jefeId + @"
),
CreditosTerminadosCTE AS (
    SELECT COUNT(CR.Cod) AS CreditosTerminados
    FROM Creditos CR
    INNER JOIN Cobrador C ON CR.Cobrador = C.Cod
    WHERE CONVERT(DATE, CR.FechaFin) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
      AND CR.Estado = 'T'
      AND C.Jefe = " + _jefeId + @"
)
SELECT 
    CONVERT(VARCHAR(12), '" + fechaInicio + @"', 103) + ' al ' + CONVERT(VARCHAR(12), '" + fechaFin + @"', 103) AS RangoFechas,
    COUNT(CA.Cobrador) AS CantidadCobradores,
    SUM(CA.CantidadBolsas) AS CantidadBolsas,
    SUM(CA.TotalSaldoActual) AS TotalSaldoActual,
    ISNULL(SUM(PA.ValorApertura), 0) + ISNULL(SUM(ENA.TotalEntregasNoApertura), 0) AS TotalEntregas,
    SUM(CA.TotalGastos) AS TotalGastos,
    SUM(CA.TotalCobrado) AS TotalCobrado,
    SUM(CA.TotalCobradoCuotas) AS TotalCobradoCuotas,
    SUM(CA.TotalCobradoExtras) AS TotalCobradoExtras,
    SUM(CA.TotalCobradoDeudas) AS TotalCobradoDeudas,
    SUM(CA.TotalPrestado) AS TotalPrestado,
    MAX(CC.CreditosCreados) AS CreditosCreados,
    MAX(CT.CreditosTerminados) AS CreditosTerminados
FROM CajasAgrupadas CA
LEFT JOIN PrimerApertura PA ON CA.Cobrador = PA.Cobrador
LEFT JOIN EntregasNoApertura ENA ON CA.Cobrador = ENA.Cobrador
CROSS JOIN CreditosCreadosCTE CC
CROSS JOIN CreditosTerminadosCTE CT
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");

            string resultado = _conexionSql.SqlJsonComand(false, comando);
            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return null;

            return JObject.Parse(resultado);
        }

        /// <summary>
        /// Obtiene las entregas de una bolsa específica
        /// </summary>
        public JArray ObtenerEntregasBolsa(int codBolsa, int cobradorId)
        {
            // Verificar que la bolsa pertenezca a un cobrador del jefe actual
            /// ValidarBolsaPerteneciente(codBolsa);

            var comando = new SqlCommand(@"
        SELECT 
            Entregas AS Descripcion,
            Cod,
            Valor,
            Fecha AS Fecha
        FROM ValoresBolsa 
        WHERE Entregas is Not Null And Entregas <> ''
          AND Bolsa = " + codBolsa.ToString() + @"
          AND Cobrador = " + cobradorId.ToString() + @" FOR JSON PATH");



            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene los créditos creados en una bolsa específica
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <returns>JArray con los créditos creados en la bolsa</returns>
        public JArray ObtenerCreditosBolsa(int codBolsa, int cobradorId)
        {
            // Verificar que la bolsa pertenezca a un cobrador del jefe actual
            /// ValidarBolsaPerteneciente(codBolsa);

            var comando = new SqlCommand(@"
    SELECT 
        VB.Credito,
        VB.Cod,
        VB.Valor,
        VB.Fecha AS Fecha,
        C.TotalPagar AS TotalPagar,
        C.NumeroDeCuotas AS NumeroDeCuotas,
        C.PorceInteres AS PorceInteres,
        CL.Nombres + ' ' + CL.Apellidos AS NombreCliente,
        CL.Documento AS DocumentoCliente
    FROM ValoresBolsa VB
    INNER JOIN Creditos C ON VB.Credito = C.Cod and C.Cobrador=VB.Cobrador
    INNER JOIN Clientes CL ON C.Cliente = CL.Cod And CL.Cobrador=C.Cobrador
    WHERE VB.Credito IS NOT NULL 
      AND VB.Credito > 0
      AND VB.Bolsa = "
            + codBolsa.ToString() + @"
      AND VB.Cobrador = " + cobradorId.ToString() + @" FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene los créditos creados en un rango de fechas, opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>JArray con los créditos creados en el rango de fechas</returns>
        public JArray ObtenerCreditosBolsaRango(string fechaInicio, string fechaFin, int? cobradorId = null)
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

            var sqlQuery = @"
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
            CO.Nombres + ' ' + CO.Apellidos AS NombreCobrador,
            CO.Cod AS CodCobrador
        FROM ValoresBolsa VB
        INNER JOIN Creditos C ON VB.Credito = C.Cod AND VB.Cobrador = C.Cobrador
        INNER JOIN Clientes CL ON C.Cliente = CL.Cod AND C.Cobrador = CL.Cobrador
        INNER JOIN Cobrador CO ON VB.Cobrador = CO.Cod
        WHERE VB.Credito IS NOT NULL 
          AND VB.Credito > 0
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND CO.Jefe = " + _jefeId;

            // Agregar filtro por cobrador si se proporciona
            if (cobradorId.HasValue && cobradorId.Value > 0)
            {
                // Verificamos que el cobrador pertenezca a este jefe
                var comandoVerificarCobrador = new SqlCommand(
                    $"SELECT COUNT(1) FROM Cobrador WHERE Cod = {cobradorId.Value} AND Jefe = {_jefeId}");

                int cobradorValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarCobrador.CommandText, true));

                if (cobradorValido == 0)
                {
                    throw new UnauthorizedAccessException("El cobrador especificado no pertenece a este jefe");
                }

                sqlQuery += @" AND CO.Cod = " + cobradorId.Value;
            }

            sqlQuery += @" ORDER BY VB.Fecha DESC
        FOR JSON PATH";

            var comando = new SqlCommand(sqlQuery);
            string jsonResult = _conexionSql.SqlJsonComand(false, comando);

            // Si no hay resultados, devolver un array vacío
            if (string.IsNullOrEmpty(jsonResult) || jsonResult == "[]")
                return new JArray();

            JArray resultado = JArray.Parse(jsonResult);
            return resultado;
        }

        /// <summary>
        /// Obtiene los gastos de una bolsa específica
        /// </summary>
        public JArray ObtenerGastosBolsa(int codBolsa, int cobradorId)
        {
            // Verificar que la bolsa pertenezca a un cobrador del jefe actual
            ///  ValidarBolsaPerteneciente(codBolsa);

            var comando = new SqlCommand(@"
        SELECT 
            Gasto AS Descripcion,
            Cod,
            Valor,
            Fecha AS Fecha
        FROM ValoresBolsa 
        WHERE Gasto is Not Null And Gasto <> ''
          AND Bolsa = " + codBolsa.ToString() + @"
          AND Cobrador = " + cobradorId.ToString() + @" FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene los gastos de una bolsa específica en un rango de fechas, opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>JArray con los gastos de la bolsa en el rango de fechas</returns>
        public JArray ObtenerGastosBolsaRango(string fechaInicio, string fechaFin, int? cobradorId = null)
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

            var sqlQuery = @"
        SELECT 
            VB.Gasto AS Descripcion,
            VB.Cod,
            VB.Valor,
            CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha,
            B.Cod AS CodBolsa,
            CB.Cod AS CodCobrador,
            CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador
        FROM ValoresBolsa VB
        INNER JOIN Bolsa B ON VB.Bolsa = B.Cod AND VB.Cobrador = B.Cobrador
        INNER JOIN Cobrador CB ON B.Cobrador = CB.Cod
        WHERE VB.Gasto IS NOT NULL 
          AND VB.Gasto <> ''
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND CB.Jefe = " + _jefeId;

            // Agregar filtro por cobrador si se proporciona
            if (cobradorId.HasValue && cobradorId.Value > 0)
            {
                // Verificamos que el cobrador pertenezca a este jefe
                var comandoVerificarCobrador = new SqlCommand(
                    $"SELECT COUNT(1) FROM Cobrador WHERE Cod = {cobradorId.Value} AND Jefe = {_jefeId}");

                int cobradorValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarCobrador.CommandText, true));

                if (cobradorValido == 0)
                {
                    throw new UnauthorizedAccessException("El cobrador especificado no pertenece a este jefe");
                }

                sqlQuery += @" AND CB.Cod = " + cobradorId.Value;
            }

            sqlQuery += @" ORDER BY VB.Fecha DESC
        FOR JSON PATH";

            var comando = new SqlCommand(sqlQuery);
            string jsonResult = _conexionSql.SqlJsonComand(false, comando);

            // Si no hay resultados, devolver un array vacío
            if (string.IsNullOrEmpty(jsonResult) || jsonResult == "[]")
                return new JArray();

            JArray resultado = JArray.Parse(jsonResult);
            return resultado;
        }

        /// <summary>
        /// Obtiene los créditos creados para los cobradores asignados a un delegado específico en un rango de fechas,
        /// opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>JArray con los créditos en el rango de fechas</returns>
        public JArray ObtenerCreditosPorDelegadoRango(string fechaInicio, string fechaFin, int? cobradorId = null)
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

            var sqlQuery = @"
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
            CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador,
            CB.Cod AS CodCobrador
        FROM ValoresBolsa VB
        INNER JOIN Creditos C ON VB.Credito = C.Cod AND VB.Cobrador = C.Cobrador
        INNER JOIN Clientes CL ON C.Cliente = CL.Cod AND C.Cobrador = CL.Cobrador
        INNER JOIN Cobrador CB ON VB.Cobrador = CB.Cod
        INNER JOIN Delegados_Cobradores DC ON CB.Cod = DC.Cobrador
        WHERE VB.Credito IS NOT NULL 
          AND VB.Credito > 0
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND DC.Delegado = " + delegadoId;

            // Agregar filtro por cobrador si se proporciona
            if (cobradorId.HasValue && cobradorId.Value > 0)
            {
                // Verificamos que el cobrador esté asignado a este delegado
                var comandoVerificarCobrador = new SqlCommand(
                    $"SELECT COUNT(1) FROM Delegados_Cobradores WHERE Delegado = {delegadoId} AND Cobrador = {cobradorId.Value}");

                int cobradorValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarCobrador.CommandText, false));

                if (cobradorValido == 0)
                {
                    throw new UnauthorizedAccessException("El cobrador especificado no está asignado a este delegado");
                }

                sqlQuery += @" AND CB.Cod = " + cobradorId.Value;
            }

            sqlQuery += @" ORDER BY VB.Fecha DESC
        FOR JSON PATH";

            var comando = new SqlCommand(sqlQuery);
            string jsonResult = _conexionSql.SqlJsonComand(false, comando);

            // Si no hay resultados, devolver un array vacío
            if (string.IsNullOrEmpty(jsonResult) || jsonResult == "[]")
                return new JArray();

            JArray resultado = JArray.Parse(jsonResult);
            return resultado;
        }


        /// <summary>
        /// Obtiene los gastos de los cobradores asignados a un delegado específico en un rango de fechas,
        /// opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>JArray con los gastos en el rango de fechas</returns>
        public JArray ObtenerGastosPorDelegadoRango(string fechaInicio, string fechaFin, int? cobradorId = null)
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

            var sqlQuery = @"
        SELECT 
            VB.Gasto AS Descripcion,
            VB.Cod,
            VB.Valor,
            CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha,
            B.Cod AS CodBolsa,
            CB.Cod AS CodCobrador,
            CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador
        FROM ValoresBolsa VB
        INNER JOIN Bolsa B ON VB.Bolsa = B.Cod AND VB.Cobrador = B.Cobrador
        INNER JOIN Cobrador CB ON B.Cobrador = CB.Cod
        INNER JOIN Delegados_Cobradores DC ON CB.Cod = DC.Cobrador
        WHERE VB.Gasto IS NOT NULL 
          AND VB.Gasto <> ''
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND DC.Delegado = " + delegadoId;

            // Agregar filtro por cobrador si se proporciona
            if (cobradorId.HasValue && cobradorId.Value > 0)
            {
                // Verificamos que el cobrador esté asignado a este delegado
                var comandoVerificarCobrador = new SqlCommand(
                    $"SELECT COUNT(1) FROM Delegados_Cobradores WHERE Delegado = {delegadoId} AND Cobrador = {cobradorId.Value}");

                int cobradorValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarCobrador.CommandText, false));

                if (cobradorValido == 0)
                {
                    throw new UnauthorizedAccessException("El cobrador especificado no está asignado a este delegado");
                }

                sqlQuery += @" AND CB.Cod = " + cobradorId.Value;
            }

            sqlQuery += @" ORDER BY VB.Fecha DESC
        FOR JSON PATH";

            var comando = new SqlCommand(sqlQuery);
            string jsonResult = _conexionSql.SqlJsonComand(false, comando);

            // Si no hay resultados, devolver un array vacío
            if (string.IsNullOrEmpty(jsonResult) || jsonResult == "[]")
                return new JArray();

            JArray resultado = JArray.Parse(jsonResult);
            return resultado;
        }

        /// <summary>
        /// Obtiene las entregas de los cobradores asignados a un delegado específico en un rango de fechas,
        /// opcionalmente filtradas por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>JArray con las entregas en el rango de fechas</returns>
        public JArray ObtenerEntregasPorDelegadoRango(string fechaInicio, string fechaFin, int? cobradorId = null)
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

            var sqlQuery = @"
        SELECT 
            VB.Entregas AS Descripcion,
            VB.Cod,
            VB.Valor,
            CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha,
            B.Cod AS CodBolsa,
            CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador,
            CB.Cod AS CodCobrador
        FROM ValoresBolsa VB
        INNER JOIN Bolsa B ON VB.Bolsa = B.Cod AND VB.Cobrador = B.Cobrador
        INNER JOIN Cobrador CB ON B.Cobrador = CB.Cod
        INNER JOIN Delegados_Cobradores DC ON CB.Cod = DC.Cobrador
        WHERE VB.Entregas IS NOT NULL 
          AND VB.Entregas <> ''
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND DC.Delegado = " + delegadoId;

            // Agregar filtro por cobrador si se proporciona
            if (cobradorId.HasValue && cobradorId.Value > 0)
            {
                // Primero verificamos que el cobrador esté asignado a este delegado
                var comandoVerificarCobrador = new SqlCommand(
                    $"SELECT COUNT(1) FROM Delegados_Cobradores WHERE Delegado = {delegadoId} AND Cobrador = {cobradorId.Value}");

                int cobradorValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarCobrador.CommandText, false));

                if (cobradorValido == 0)
                {
                    throw new UnauthorizedAccessException("El cobrador especificado no está asignado a este delegado");
                }

                sqlQuery += @" AND CB.Cod = " + cobradorId.Value;
            }

            sqlQuery += @" ORDER BY VB.Fecha DESC
        FOR JSON PATH";

            var comando = new SqlCommand(sqlQuery);
            string jsonResult = _conexionSql.SqlJsonComand(false, comando);

            // Si no hay resultados, devolver un array vacío
            if (string.IsNullOrEmpty(jsonResult) || jsonResult == "[]")
                return new JArray();

            JArray resultado = JArray.Parse(jsonResult);
            return resultado;
        }

        /// <summary>
        /// Obtiene las entregas de todas las bolsas del jefe actual entre dos fechas, opcionalmente filtradas por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha de inicio en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha de fin en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>Lista de entregas en formato JSON</returns>
        public JArray ObtenerEntregasRango(string fechaInicio, string fechaFin, int? cobradorId = null)
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

            // Construcción de la consulta base con validación doble en los JOINs
            var sqlQuery = @"
        SELECT 
            VB.Entregas AS Descripcion,
            VB.Cod,
            VB.Valor,
            CONVERT(VARCHAR(12), VB.Fecha, 103) AS Fecha,
            B.Cobrador AS CodCobrador,
            CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador
        FROM ValoresBolsa VB
        INNER JOIN Bolsa B ON VB.Bolsa = B.Cod AND VB.Cobrador = B.Cobrador
        INNER JOIN Cobrador CB ON B.Cobrador = CB.Cod
        WHERE VB.Entregas IS NOT NULL 
          AND VB.Entregas <> ''
          AND CONVERT(DATE, VB.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND CB.Jefe = " + _jefeId;

            // Agregar filtro por cobrador si se proporciona
            if (cobradorId.HasValue && cobradorId.Value > 0)
            {
                sqlQuery += @" AND B.Cobrador = " + cobradorId.Value;
            }

            sqlQuery += @" ORDER BY VB.Fecha DESC
        FOR JSON PATH";

            var comando = new SqlCommand(sqlQuery);
            string jsonResult = _conexionSql.SqlJsonComand(false, comando);

            // Si no hay resultados, devolver un array vacío
            if (string.IsNullOrEmpty(jsonResult) || jsonResult == "[]")
                return new JArray();

            JArray resultado = JArray.Parse(jsonResult);
            return resultado;
        }

        /// <summary>
        /// Obtiene los pagos de una bolsa específica
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <returns>JArray con los pagos de la bolsa</returns>
        public JArray ObtenerPagosBolsa(int codBolsa, int cobradorId)
        {

            var comando = new SqlCommand(@"
        SELECT 
            RD.Cod,
            RD.Credito,
            RD.Fecha,
            RD.Visitado,
            RD.Valor,
            RD.Descripcion,
            RD.Lat,
            RD.Long,
            RD.Fecha AS FechaFormateada,
            C.Nombres + ' ' + C.Apellidos AS NombreCliente,
            C.Documento AS DocumentoCliente
        FROM RegDiarioCuotas RD
        INNER JOIN Creditos CR ON RD.Credito = CR.Cod And CR.Cobrador=RD.Cobrador
        INNER JOIN Clientes C ON CR.Cliente = C.Cod And C.Cobrador= CR.Cobrador
        WHERE RD.Bolsa = " + codBolsa.ToString() + @"
        AND RD.Cobrador = " + cobradorId.ToString() + @" AND RD.Valor > 0 
        ORDER BY RD.Fecha DESC FOR JSON PATH");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene los pagos realizados en un rango de fechas para el jefe actual,
        /// opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>JArray con los pagos en el rango de fechas</returns>
        public JArray ObtenerPagosRango(string fechaInicio, string fechaFin, int? cobradorId = null)
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

            var sqlQuery = @"
        SELECT 
            RD.Cod,
            RD.Credito,
            RD.Fecha,
            RD.Visitado,
            RD.Valor,
            RD.Descripcion,
            RD.Fecha AS FechaFormateada,
            C.Nombres + ' ' + C.Apellidos AS NombreCliente,
            C.Documento AS DocumentoCliente,
            CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador,
            CB.Cod AS CodCobrador,
            B.Cod AS CodBolsa
        FROM RegDiarioCuotas RD
        INNER JOIN Bolsa B ON RD.Bolsa = B.Cod AND RD.Cobrador = B.Cobrador
        INNER JOIN Cobrador CB ON B.Cobrador = CB.Cod
        INNER JOIN Creditos CR ON RD.Credito = CR.Cod AND RD.Cobrador = CR.Cobrador
        INNER JOIN Clientes C ON CR.Cliente = C.Cod AND CR.Cobrador = C.Cobrador
        WHERE CONVERT(DATE, RD.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND CB.Jefe = " + _jefeId;

            // Agregar filtro por cobrador si se proporciona
            if (cobradorId.HasValue && cobradorId.Value > 0)
            {
                // Verificamos que el cobrador pertenezca a este jefe
                var comandoVerificarCobrador = new SqlCommand(
                    $"SELECT COUNT(1) FROM Cobrador WHERE Cod = {cobradorId.Value} AND Jefe = {_jefeId}");

                int cobradorValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarCobrador.CommandText, true));

                if (cobradorValido == 0)
                {
                    throw new UnauthorizedAccessException("El cobrador especificado no pertenece a este jefe");
                }

                sqlQuery += @" AND CB.Cod = " + cobradorId.Value;
            }

            sqlQuery += @" ORDER BY RD.Fecha DESC
        FOR JSON PATH";

            var comando = new SqlCommand(sqlQuery);
            string jsonResult = _conexionSql.SqlJsonComand(false, comando);

            // Si no hay resultados, devolver un array vacío
            if (string.IsNullOrEmpty(jsonResult) || jsonResult == "[]")
                return new JArray();

            JArray resultado = JArray.Parse(jsonResult);
            return resultado;
        }

        /// <summary>
        /// Obtiene los pagos realizados en un rango de fechas para los cobradores asignados a un delegado específico,
        /// opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>JArray con los pagos en el rango de fechas</returns>
        public JArray ObtenerPagosPorDelegadoRango(string fechaInicio, string fechaFin, int? cobradorId = null)
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

            var sqlQuery = @"
        SELECT 
            RD.Cod,
            RD.Credito,
            RD.Fecha,
            RD.Visitado,
            RD.Valor,
            RD.Descripcion,
            RD.Fecha AS FechaFormateada,
            C.Nombres + ' ' + C.Apellidos AS NombreCliente,
            C.Documento AS DocumentoCliente,
            CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador,
            CB.Cod AS CodCobrador,
            B.Cod AS CodBolsa
        FROM RegDiarioCuotas RD
        INNER JOIN Bolsa B ON RD.Bolsa = B.Cod AND RD.Cobrador = B.Cobrador
        INNER JOIN Cobrador CB ON B.Cobrador = CB.Cod
        INNER JOIN Creditos CR ON RD.Credito = CR.Cod AND RD.Cobrador = CR.Cobrador
        INNER JOIN Clientes C ON CR.Cliente = C.Cod AND CR.Cobrador = C.Cobrador
        INNER JOIN Delegados_Cobradores DC ON CB.Cod = DC.Cobrador
        WHERE CONVERT(DATE, RD.Fecha) BETWEEN '" + fechaInicio + @"' AND '" + fechaFin + @"'
          AND DC.Delegado = " + delegadoId;

            // Agregar filtro por cobrador si se proporciona
            if (cobradorId.HasValue && cobradorId.Value > 0)
            {
                // Verificamos que el cobrador esté asignado a este delegado
                var comandoVerificarCobrador = new SqlCommand(
                    $"SELECT COUNT(1) FROM Delegados_Cobradores WHERE Delegado = {delegadoId} AND Cobrador = {cobradorId.Value}");

                int cobradorValido = Convert.ToInt32(_conexionSql.TraerDato(comandoVerificarCobrador.CommandText, false));

                if (cobradorValido == 0)
                {
                    throw new UnauthorizedAccessException("El cobrador especificado no está asignado a este delegado");
                }

                sqlQuery += @" AND CB.Cod = " + cobradorId.Value;
            }

            sqlQuery += @" ORDER BY RD.Fecha DESC
        FOR JSON PATH";

            var comando = new SqlCommand(sqlQuery);
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
        /// private void ValidarBolsaPerteneciente(int codBolsa)
        /// {
        /// var _jefeId = ObtenerId();
        ///  var comando = new SqlCommand(@"
        /// SELECT COUNT(1) 
        /// FROM Bolsa B
        /// INNER JOIN Cobrador C ON B.Cobrador = C.Cod
        /// WHERE B.Cod = "+codBolsa+" AND C.Jefe ="+_jefeId.ToString() + " for json path");

        /// int count = Convert.ToInt32(_conexionSql.TraerDato(comando.CommandText, true));

        ///   if (count == 0)
        ///  {
        ///      throw new UnauthorizedAccessException("La bolsa especificada no pertenece a un cobrador de este jefe");
        ///}
        ///  }
    }
}