using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;
using Microsoft.Data.SqlClient;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;

namespace ApiEasyPay.Aplication.Services
{
        public class CreditosService
        {
        private readonly ConexionSql _conexionSql;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CreditosService(ConexionSql conexionSql, IHttpContextAccessor httpContextAccessor)
        {
            _conexionSql = conexionSql;
            _httpContextAccessor = httpContextAccessor;

            // Configurar cadena de conexión principal
            _conexionSql.BdPrincipal = ConfigurationOptions.Instance.StrConexBdSql;

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
        /// Valida que el cobrador pertenezca al jefe actual
        /// </summary>
        private void ValidarCobradorPerteneciente(int cobradorId)
        {
            var _jefeId = ObtenerId();
            var comando = new SqlCommand("SELECT COUNT(1) FROM Cobrador WHERE Cod = "+cobradorId.ToString()+" AND Jefe ="+_jefeId);

            int count = Convert.ToInt32(_conexionSql.TraerDato(comando.CommandText, true));

            if (count == 0)
            {
                throw new UnauthorizedAccessException("El cobrador especificado no pertenece a este jefe");
            }
        }

        /// <summary>
        /// Obtiene un resumen estadístico de créditos para el jefe actual
        /// </summary>
        public JArray ObtenerResumenCreditosJefe()
        {
            var _jefeId = ObtenerId();
            var comando = new SqlCommand(@"
                SELECT 
                    COUNT(CR.Cod) AS CreditosCreados,
                    SUM(CASE WHEN CR.Estado = 'V' THEN CR.Total ELSE 0 END) AS TotalPrestadoV,
                    SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagado ELSE 0 END) AS TotalPagadoV,
                    SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagar ELSE 0 END) AS TotalPagarV,
                    SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagar - CR.TotalPagado ELSE 0 END) AS SaldoPrestadoV,
                    SUM(CASE WHEN CR.Estado = 'V' THEN 1 ELSE 0 END) AS CreditosVigentes,
                    SUM(CASE WHEN CR.Estado = 'T' THEN CR.Total ELSE 0 END) AS TotalPrestadoT,
                    SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagado ELSE 0 END) AS TotalPagadoT,
                    SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagar ELSE 0 END) AS TotalPagarT,
                    SUM(CASE WHEN CR.Estado = 'T' THEN 1 ELSE 0 END) AS CreditosTerminados,
                    SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagar - CR.TotalPagado ELSE 0 END) AS SaldoPrestadoT
                FROM Creditos CR 
                INNER JOIN Cobrador C ON C.Cod = CR.Cobrador 
                WHERE C.Jefe = "+_jefeId+" for json path");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene un resumen estadístico de créditos para el delegado actual
        /// </summary>
        public JArray ObtenerResumenCreditosDelegado()
        {
            var _delegadoId = ObtenerId();
            var comando = new SqlCommand(@"
        SELECT 
            COUNT(CR.Cod) AS CreditosCreados,
            SUM(CASE WHEN CR.Estado = 'V' THEN CR.Total ELSE 0 END) AS TotalPrestadoV,
            SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagado ELSE 0 END) AS TotalPagadoV,
            SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagar ELSE 0 END) AS TotalPagarV,
            SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagar - CR.TotalPagado ELSE 0 END) AS SaldoPrestadoV,
            SUM(CASE WHEN CR.Estado = 'V' THEN 1 ELSE 0 END) AS CreditosVigentes,
            SUM(CASE WHEN CR.Estado = 'T' THEN CR.Total ELSE 0 END) AS TotalPrestadoT,
            SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagado ELSE 0 END) AS TotalPagadoT,
            SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagar ELSE 0 END) AS TotalPagarT,
            SUM(CASE WHEN CR.Estado = 'T' THEN 1 ELSE 0 END) AS CreditosTerminados,
            SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagar - CR.TotalPagado ELSE 0 END) AS SaldoPrestadoT
        FROM Creditos CR 
        INNER JOIN Cobrador C ON C.Cod = CR.Cobrador 
        INNER JOIN Delegados_Cobradores DC ON DC.Cobrador = C.Cod
        WHERE DC.Delegado = " + _delegadoId + " for json path");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene estadísticas de créditos agrupadas por cobrador asignado al delegado
        /// </summary>
        public JArray ObtenerResumenPorCobradorDelegado()
        {
            var _delegadoId = ObtenerId();
            var comando = new SqlCommand(@"
        SELECT 
            C.Cod AS CodCobrador,
            C.Nombres + ' ' + C.Apellidos AS Nombres,
            COUNT(CR.Cod) AS CreditosCreados,
            SUM(CASE WHEN CR.Estado = 'V' THEN 1 ELSE 0 END) AS CreditosVigentes,
            SUM(CASE WHEN CR.Estado = 'V' THEN CR.Total ELSE 0 END) AS TotalPrestadoV,
            SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagar ELSE 0 END) AS TotalPagarV,
            SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagado ELSE 0 END) AS TotalPagadoV,
            SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagar - CR.TotalPagado ELSE 0 END) AS SaldoPrestadoV,
            SUM(CASE WHEN CR.Estado = 'T' THEN 1 ELSE 0 END) AS CreditosTerminados,
            SUM(CASE WHEN CR.Estado = 'T' THEN CR.Total ELSE 0 END) AS TotalPrestadoT,
            SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagado ELSE 0 END) AS TotalPagadoT,
            SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagar ELSE 0 END) AS TotalPagarT,
            0 AS CarteraEsperada,
            0 AS Cartera,
            SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagar - CR.TotalPagado ELSE 0 END) AS SaldoPrestadoT
        FROM Cobrador C
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        LEFT JOIN Creditos CR ON C.Cod = CR.Cobrador
        WHERE DC.Delegado = " + _delegadoId + @"
        GROUP BY C.Cod, C.Nombres, C.Apellidos for json path");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene estadísticas de créditos agrupadas por cobrador
        /// </summary>
        public JArray ObtenerResumenPorCobrador()
        {
            var _jefeId = ObtenerId();
            var comando = new SqlCommand(@"
                SELECT 
                    C.Cod AS CodCobrador,
                    C.Nombres + ' ' + C.Apellidos AS Nombres,
                    COUNT(CR.Cod) AS CreditosCreados,
                    SUM(CASE WHEN CR.Estado = 'V' THEN 1 ELSE 0 END) AS CreditosVigentes,
                    SUM(CASE WHEN CR.Estado = 'V' THEN CR.Total ELSE 0 END) AS TotalPrestadoV,
                    SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagar ELSE 0 END) AS TotalPagarV,
                    SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagado ELSE 0 END) AS TotalPagadoV,
                    SUM(CASE WHEN CR.Estado = 'V' THEN CR.TotalPagar - CR.TotalPagado ELSE 0 END) AS SaldoPrestadoV,
                    SUM(CASE WHEN CR.Estado = 'T' THEN 1 ELSE 0 END) AS CreditosTerminados,
                    SUM(CASE WHEN CR.Estado = 'T' THEN CR.Total ELSE 0 END) AS TotalPrestadoT,
                    SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagado ELSE 0 END) AS TotalPagadoT,
                    SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagar ELSE 0 END) AS TotalPagarT,
                    0 AS CarteraEsperada,
                    0 AS Cartera,
                    SUM(CASE WHEN CR.Estado = 'T' THEN CR.TotalPagar - CR.TotalPagado ELSE 0 END) AS SaldoPrestadoT
                FROM Cobrador C
                LEFT JOIN Creditos CR ON C.Cod = CR.Cobrador
                WHERE C.Jefe = " + _jefeId+@"
                GROUP BY C.Cod, C.Nombres, C.Apellidos for json path");
            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene la lista de créditos vigentes para un cobrador específico
        /// </summary>
        /// <param name="cobradorId">ID del cobrador</param>
        public JArray ObtenerCreditosVigentes(int cobradorId)
        {
            // Primero verificamos que el cobrador pertenezca al jefe actual
            ValidarCobradorPerteneciente(cobradorId);

            var comando = new SqlCommand(@"
                SELECT 
                    CASE Cr.TipoCredito WHEN 1 THEN 'Amortizable' WHEN 0 THEN 'Fijo' END AS Tipo,
                    ISNULL(Cr.NumeroDeCuotas - Cr.CuotaActual, 0) AS CuotasP, 
                    Cl.Nombres + ' ' + Cl.Apellidos AS Cliente, 
                    Cl.Documento AS DniCliente,
                    Cr.Cliente AS CodCliente,
                    Cr.Cod AS CodCredito,
                    Cr.Descripcion, 
                    Cr.Total AS Valor, 
                    Cr.PorceInteres AS PorceInteres, 
                    Cr.TotalPagar AS TotalPagar, 
                    Cr.NumeroDeCuotas AS NumCuotas, 
                    Cr.ValorCuota AS ValCuota, 
                    Cr.CuotaActual AS CuotasPagadas, 
                    Cr.TotalPagado AS TotalPagado,
                    Cr.TotalPagar - Cr.TotalPagado AS Saldo,
                    CONVERT(VARCHAR(10), Cr.FechaInicio, 103) AS FechaInicio,
                    CASE Cr.Estado 
                        WHEN 'V' THEN 'Vigente' 
                        WHEN 'A' THEN 'Anulado' 
                        WHEN 'T' THEN 'Terminado' 
                    END AS Estado,
                    Cr.Estado AS EstadoN,
                    CONVERT(VARCHAR(10), Cr.ProximoPago, 103) AS ProximoPago,
                    CONVERT(VARCHAR(12), ISNULL(Cr.OrdenRuta, 0)) AS OrdenRuta
                FROM Creditos Cr 
                INNER JOIN Clientes Cl ON Cr.Cliente = Cl.Cod AND Cr.Cobrador = Cl.Cobrador
                INNER JOIN Cobrador CB ON CB.Cod = Cr.Cobrador
                WHERE Cr.Estado = 'V' AND Cr.Cobrador = "+cobradorId.ToString()+@"
                ORDER BY Cr.OrdenRuta ASC for json path");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene la lista de créditos terminados para un cobrador específico
        /// </summary>
        /// <param name="cobradorId">ID del cobrador</param>
        public JArray ObtenerCreditosTerminados(int cobradorId)
        {
            // Verificar que el cobrador pertenezca al jefe actual
            ValidarCobradorPerteneciente(cobradorId);

            var comando = new SqlCommand(@"
                SELECT 
                    CASE Cr.TipoCredito WHEN 1 THEN 'Amortizable' WHEN 0 THEN 'Fijo' END AS Tipo,
                    ISNULL(Cr.NumeroDeCuotas - Cr.CuotaActual, 0) AS CuotasP, 
                    Cl.Nombres + ' ' + Cl.Apellidos AS Cliente, 
                    Cl.Documento AS DniCliente,
                    ISNULL(Cr.OrdenRuta, 0) AS Orden,
                    Cr.Cod AS CodCredito,
                    Cr.Descripcion, 
                    Cr.Total AS Valor, 
                    Cr.PorceInteres AS PorceInteres, 
                    Cr.TotalPagar AS TotalPagar, 
                    Cr.NumeroDeCuotas AS NumCuotas, 
                    Cr.ValorCuota AS ValCuota, 
                    Cr.CuotaActual AS CuotasPagadas, 
                    Cr.TotalPagado AS TotalPagado,
                    Cr.TotalPagar - Cr.TotalPagado AS Saldo,
                    CONVERT(VARCHAR(10), Cr.FechaInicio, 103) AS FechaInicio,
                    CASE Cr.Estado 
                        WHEN 'V' THEN 'Vigente' 
                        WHEN 'A' THEN 'Anulado' 
                        WHEN 'T' THEN 'Terminado' 
                    END AS Estado,
                    Cr.Estado AS EstadoN,
                    CONVERT(VARCHAR(10), Cr.ProximoPago, 103) AS ProximoPago,
                    CONVERT(VARCHAR(10), Cr.FechaFin, 103) AS FechaFin
                FROM Creditos Cr 
                INNER JOIN Clientes Cl ON Cr.Cliente = Cl.Cod AND Cr.Cobrador = Cl.Cobrador
                INNER JOIN Cobrador CB ON CB.Cod = Cr.Cobrador
                WHERE Cr.Estado = 'T' AND Cr.Cobrador = "+cobradorId.ToString()+@"
                ORDER BY Cr.Cod DESC for json path");


            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene detalles de un crédito específico para un cobrador determinado
        /// </summary>
        /// <param name="creditoId">ID del crédito</param>
        /// <param name="cobradorId">ID del cobrador</param>
        public JObject ObtenerDetalleCredito(int creditoId, int cobradorId)
        {
            // Verificar que el cobrador pertenezca al jefe actual
            ValidarCobradorPerteneciente(cobradorId);

            var comando = new SqlCommand(@"
                SELECT 
                    Cr.*,
                    Cl.Nombres + ' ' + Cl.Apellidos AS NombreCliente,
                    Cl.Documento AS DocumentoCliente,
                    Cl.Telefono AS TelefonoCliente,
                    CB.Nombres + ' ' + CB.Apellidos AS NombreCobrador
                FROM Creditos Cr 
                INNER JOIN Clientes Cl ON Cr.Cliente = Cl.Cod AND Cr.Cobrador = Cl.Cobrador
                INNER JOIN Cobrador CB ON CB.Cod = Cr.Cobrador
                WHERE Cr.Cod = "+creditoId.ToString()+" AND Cr.Cobrador = "+cobradorId.ToString()+@"
                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER");

            string resultado = _conexionSql.SqlJsonComand(false, comando);
            if (string.IsNullOrEmpty(resultado) || resultado == "[]")
                return null;

            return JObject.Parse(resultado);
        }

        /// <summary>
        /// Obtiene las cuotas de un crédito específico para un cobrador determinado
        /// </summary>
        /// <param name="creditoId">ID del crédito</param>
        /// <param name="cobradorId">ID del cobrador</param>
        public JArray ObtenerCuotasCredito(int creditoId, int cobradorId)
        {
            // Verificar que el cobrador pertenezca al jefe actual
            ValidarCobradorPerteneciente(cobradorId);

            var comando = new SqlCommand(@"
                SELECT 
                    Cu.*,
                    CONVERT(VARCHAR(10), Cu.Fecha, 103) AS FechaFormateada,
                    CASE Cu.Estado 
                        WHEN 'PA' THEN 'Pagada' 
                        WHEN 'PE' THEN 'Pendiente' 
                        WHEN 'C' THEN 'Cancelada' 
                    END AS EstadoDescripcion
                FROM Cuotas Cu
                WHERE Cu.Credito = "+creditoId.ToString()+" AND Cu.Cobrador = "+cobradorId.ToString()+@"
                ORDER BY Cu.NumCuota ASC for json path");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }

        /// <summary>
        /// Obtiene el historial de pagos de un crédito específico para un cobrador determinado
        /// </summary>
        /// <param name="creditoId">ID del crédito</param>
        /// <param name="cobradorId">ID del cobrador</param>
        public JArray ObtenerHistorialCredito(int creditoId, int cobradorId)
        {
            // Verificar que el cobrador pertenezca al jefe actual
            ValidarCobradorPerteneciente(cobradorId);

            var comando = new SqlCommand(@"
    SELECT 
        RD.*,
        B.Estado AS EstadoBolsa,
        CONVERT(VARCHAR(10), RD.Fecha, 103) AS FechaFormateada
    FROM RegDiarioCuotas RD
    INNER JOIN Bolsa B ON RD.Bolsa = B.Cod AND RD.Cobrador = B.Cobrador
    WHERE RD.Credito = " + creditoId.ToString() + " AND RD.Cobrador = " + cobradorId.ToString() + @"
    ORDER BY RD.Fecha DESC for json path");


            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
            JArray resultado = JArray.Parse(jsonResult);

            return resultado;
        }
    }
}