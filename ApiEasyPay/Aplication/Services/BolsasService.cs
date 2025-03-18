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
        public JObject ObtenerDatosBolsaPorDelegado( string fecha)
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

            // Consulta para obtener resumen de bolsas por delegado
            var comando = new SqlCommand(@"
        SELECT 
            CONVERT(VARCHAR(12), '" + fecha + @"', 103) AS Fecha, 
            ISNULL((
                SELECT SUM(B.SaldoActual) AS Total 
                FROM Bolsa B 
                INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
                INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
                WHERE B.Estado = 'A' 
                  AND DC.Delegado = " + delegadoId + @"
            ), 0) AS TotalBolsasDelegado, 
            ISNULL((
                SELECT COUNT(B.Cod) 
                FROM Bolsa B 
                INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
                INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
                WHERE B.Estado = 'A' 
                  AND DC.Delegado = " + delegadoId + @"
            ), 0) AS CantidadBolsasActivas,
            ISNULL((
                SELECT COUNT(B.Cod) 
                FROM Bolsa B 
                INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
                INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
                WHERE B.Estado = 'C' 
                  AND DC.Delegado = " + delegadoId + @"
            ), 0) AS CantidadBolsasCerradas,
            (
                SELECT D.Nombres + ' ' + D.Apellidos 
                FROM Delegado D 
                WHERE D.Cod = " + delegadoId + @"
            ) AS NombreDelegado
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
            D.Cod AS CodDelegado,
            D.Nombres + ' ' + D.Apellidos AS NombreDelegado
        FROM Bolsa B 
        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
        INNER JOIN Delegados_Cobradores DC ON C.Cod = DC.Cobrador
        INNER JOIN Delegado D ON DC.Delegado = D.Cod
        WHERE B.Estado = 'A' 
          AND DC.Delegado = " + delegadoId + " FOR JSON PATH");

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
                    CONVERT(VARCHAR(12), B.FechaInicio, 103) AS FechaInicio
                FROM Bolsa B 
                INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
                WHERE B.Estado = 'A' AND C.Jefe = " + _jefeId + " for json path");

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
            var comando = new SqlCommand(@"
                SELECT 
                    CONVERT(VARCHAR(12),"+ fecha + @", 103) AS Fecha, 
                    ISNULL((
                        SELECT SUM(B.SaldoActual) AS Total 
                        FROM Bolsa B 
                        INNER JOIN Cobrador C ON B.Cobrador = C.Cod 
                        WHERE B.Estado = 'A' AND C.Jefe = "+ _jefeId + @"
                    ), 0) AS TotalBolsaA, 
                    ISNULL((
                        SELECT Monto 
                        FROM FondoInversion 
                        WHERE Jefe = "+ _jefeId + @"
                    ), 0) AS TotalBolsaC
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
                WHERE Credito IS NULL 
                  AND Gasto IS NULL 
                  AND Bolsa ="+ codBolsa.ToString() + " for json path");



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
                WHERE Credito IS NULL 
                  AND Entregas IS NULL 
                  AND Bolsa ="+ codBolsa.ToString() + " for json path");

            string jsonResult = _conexionSql.SqlJsonComand(false, comando);
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