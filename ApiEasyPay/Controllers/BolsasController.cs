using ApiEasyPay.Aplication.Services;
using ApiEasyPay.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiEasyPay.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [TokenAuthorization]
    public class BolsasController : ControllerBase
    {
        private readonly BolsasService _bolsaService;

        public BolsasController(BolsasService bolsaService)
        {
            _bolsaService = bolsaService;
        }

        /// <summary>
        /// Obtiene datos resumidos de bolsa para la fecha indicada
        /// </summary>
        /// <param name="fecha">Fecha en formato yyyy-MM-dd</param>
        /// <returns>Datos de la bolsa en formato JSON</returns>
        [HttpGet("datos")]
        public IActionResult GetDatosBolsaByFecha([FromQuery] string fecha)
        {
            try
            {
                if (string.IsNullOrEmpty(fecha))
                {
                    fecha = DateTime.Now.ToString("yyyy-MM-dd");
                }

                var resultado = _bolsaService.ObtenerDatosBolsaByFecha(fecha);
                if (resultado == null)
                    return NotFound(new { mensaje = "No se encontraron datos para la fecha especificada" });

                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener datos de bolsa: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene datos resumidos de bolsa para los cobradores de un delegado específico en la fecha indicada
        /// </summary>
        /// <param name="fecha">Fecha en formato yyyy-MM-dd (opcional)</param>
        /// <returns>Datos de las bolsas en formato JSON</returns>
        [HttpGet("delegado/datos")]
        public IActionResult GetDatosBolsaByFechaPorDelegado([FromQuery] string fecha)
        {
            try
            {
                var resultado = _bolsaService.ObtenerDatosBolsaPorDelegado(fecha);
                if (resultado == null)
                    return NotFound(new { mensaje = "No se encontraron datos para el delegado y fecha especificados" });

                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener datos de bolsa por delegado: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene datos resumidos de bolsa para un rango de fechas específico
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>Datos agregados de las bolsas en el rango de fechas especificado en formato JSON</returns>
        [HttpGet("datos-rango")]
        public IActionResult GetDatosBolsaRango([FromQuery] string fechaInicio, [FromQuery] string fechaFin)
        {
            try
            {
                var resultado = _bolsaService.ObtenerDatosBolsaRango(fechaInicio, fechaFin);
                if (resultado == null)
                    return NotFound(new { mensaje = "No se encontraron datos para el rango de fechas especificado" });

                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener datos de bolsa por rango de fechas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene datos resumidos de bolsa para los cobradores de un delegado específico en un rango de fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>Datos agregados de las bolsas en el rango de fechas especificado en formato JSON</returns>
        [HttpGet("delegado/datos-rango")]
        public IActionResult GetDatosBolsaPorDelegadoRango([FromQuery] string fechaInicio, [FromQuery] string fechaFin)
        {
            try
            {
                var resultado = _bolsaService.ObtenerDatosBolsaPorDelegadoRango(fechaInicio, fechaFin);
                if (resultado == null)
                    return NotFound(new { mensaje = "No se encontraron datos para el delegado y rango de fechas especificados" });

                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener datos de bolsa por delegado en rango de fechas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas del jefe actual
        /// </summary>
        /// <returns>Lista de bolsas abiertas en formato JSON</returns>
        [HttpGet("ByFecha")]
        public IActionResult GetBolsasByFecha([FromQuery] string fecha)
        {
            try
            {
                if (string.IsNullOrEmpty(fecha))
                {
                    fecha = DateTime.Now.ToString("yyyy-MM-dd");
                }
                var resultado = _bolsaService.ObtenerBolsasxFecha(fecha);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener bolsas abiertas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas del delegado actual
        /// </summary>
        /// <returns>Lista de bolsas abiertas en formato JSON</returns>
        [HttpGet("delegado/ByFecha")]
        public IActionResult GetBolsasByFechaDelegado([FromQuery] string fecha)
        {
            try
            {
                if (string.IsNullOrEmpty(fecha))
                {
                    fecha = DateTime.Now.ToString("yyyy-MM-dd");
                }
                var resultado = _bolsaService.ObtenerBolsasPorDelegadoxFecha(fecha);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener bolsas abiertas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas del jefe actual en un rango de fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>Lista de bolsas abiertas en el rango de fechas en formato JSON</returns>
        [HttpGet("bolsas-rango")]
        public IActionResult GetBolsasRango([FromQuery] string fechaInicio, [FromQuery] string fechaFin)
        {
            try
            {
                var resultado = _bolsaService.ObtenerBolsasRango(fechaInicio, fechaFin);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener bolsas abiertas en rango de fechas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene un resumen de las bolsas abiertas de los cobradores asignados a un delegado específico en un rango de fechas
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <returns>Lista de bolsas abiertas en el rango de fechas en formato JSON</returns>
        [HttpGet("delegado/bolsas-rango")]
        public IActionResult GetBolsasPorDelegadoRango([FromQuery] string fechaInicio, [FromQuery] string fechaFin)
        {
            try
            {
                var resultado = _bolsaService.ObtenerBolsasPorDelegadoRango(fechaInicio, fechaFin);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener bolsas abiertas por delegado en rango de fechas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene las entregas de una bolsa específica para un cobrador
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <param name="cobradorId">Código del cobrador</param>
        /// <returns>Lista de entregas en formato JSON</returns>
        [HttpGet("{codBolsa}/entregas/{cobradorId}")]
        public IActionResult GetEntregasBolsa(int codBolsa, int cobradorId)
        {
            try
            {
                var resultado = _bolsaService.ObtenerEntregasBolsa(codBolsa, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener entregas de bolsa: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene las entregas de todas las bolsas del jefe actual entre dos fechas, opcionalmente filtradas por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>Lista de entregas en formato JSON</returns>
        [HttpGet("entregas-rango")]
        public IActionResult GetEntregasRango(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin,
            [FromQuery] int? cobradorId = null)
        {
            try
            {
                var resultado = _bolsaService.ObtenerEntregasRango(fechaInicio, fechaFin, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener entregas en rango de fechas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene las entregas de todas las bolsas de los cobradores asignados a un delegado entre dos fechas, 
        /// opcionalmente filtradas por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>Lista de entregas en formato JSON</returns>
        [HttpGet("delegado/entregas-rango")]
        public IActionResult GetEntregasPorDelegadoRango(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin,
            [FromQuery] int? cobradorId = null)
        {
            try
            {
                var resultado = _bolsaService.ObtenerEntregasPorDelegadoRango(fechaInicio, fechaFin, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener entregas por delegado en rango de fechas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene los gastos de una bolsa específica para un cobrador
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <param name="cobradorId">Código del cobrador</param>
        /// <returns>Lista de gastos en formato JSON</returns>
        [HttpGet("{codBolsa}/gastos/{cobradorId}")]
        public IActionResult GetGastosBolsa(int codBolsa, int cobradorId)
        {
            try
            {
                var resultado = _bolsaService.ObtenerGastosBolsa(codBolsa, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener gastos de bolsa: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene los gastos de una bolsa específica en un rango de fechas, opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>Lista de gastos en formato JSON</returns>
        [HttpGet("gastos-rango")]
        public IActionResult GetGastosBolsaRango(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin,
            [FromQuery] int? cobradorId = null)
        {
            try
            {
                var resultado = _bolsaService.ObtenerGastosBolsaRango(fechaInicio, fechaFin, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener gastos de bolsa en rango de fechas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene los gastos de los cobradores asignados a un delegado específico en un rango de fechas,
        /// opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>Lista de gastos en formato JSON</returns>
        [HttpGet("delegado/gastos-rango")]
        public IActionResult GetGastosPorDelegadoRango(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin,
            [FromQuery] int? cobradorId = null)
        {
            try
            {
                var resultado = _bolsaService.ObtenerGastosPorDelegadoRango(fechaInicio, fechaFin, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener gastos por delegado en rango de fechas: {ex.Message}" });
            }
        }


        /// <summary>
        /// Obtiene los créditos creados en una bolsa específica
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <returns>Lista de créditos en formato JSON</returns>
        [HttpGet("{codBolsa}/creditos/{cobradorId}")]
        public IActionResult GetCreditosBolsa(int codBolsa, int cobradorId)
        {
            try
            {
                var resultado = _bolsaService.ObtenerCreditosBolsa(codBolsa, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener créditos de bolsa: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene los créditos creados en un rango de fechas, opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>Lista de créditos en formato JSON</returns>
        [HttpGet("creditos-rango")]
        public IActionResult GetCreditosBolsaRango(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin,
            [FromQuery] int? cobradorId = null)
        {
            try
            {
                var resultado = _bolsaService.ObtenerCreditosBolsaRango(fechaInicio, fechaFin, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener créditos en rango de fechas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene los créditos creados para los cobradores asignados a un delegado específico en un rango de fechas,
        /// opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>Lista de créditos en formato JSON</returns>
        [HttpGet("delegado/creditos-rango")]
        public IActionResult GetCreditosPorDelegadoRango(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin,
            [FromQuery] int? cobradorId = null)
        {
            try
            {
                var resultado = _bolsaService.ObtenerCreditosPorDelegadoRango(fechaInicio, fechaFin, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener créditos por delegado en rango de fechas: {ex.Message}" });
            }
        }





        /// <summary>
        /// Obtiene los pagos de una bolsa específica para un cobrador
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <param name="cobradorId">Código del cobrador</param>
        /// <returns>Lista de pagos en formato JSON</returns>
        [HttpGet("{codBolsa}/pagos/{cobradorId}")]
        public IActionResult GetPagosBolsa(int codBolsa, int cobradorId)
        {
            try
            {
                var resultado = _bolsaService.ObtenerPagosBolsa(codBolsa, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener pagos de bolsa: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene los pagos realizados en un rango de fechas para el jefe actual,
        /// opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>Lista de pagos en formato JSON</returns>
        [HttpGet("pagos-rango")]
        public IActionResult GetPagosRango(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin,
            [FromQuery] int? cobradorId = null)
        {
            try
            {
                var resultado = _bolsaService.ObtenerPagosRango(fechaInicio, fechaFin, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener pagos en rango de fechas: {ex.Message}" });
            }
        }


        /// <summary>
        /// Obtiene los pagos realizados en un rango de fechas para los cobradores asignados a un delegado específico,
        /// opcionalmente filtrados por cobrador
        /// </summary>
        /// <param name="fechaInicio">Fecha inicial en formato yyyy-MM-dd</param>
        /// <param name="fechaFin">Fecha final en formato yyyy-MM-dd</param>
        /// <param name="cobradorId">Código del cobrador (opcional)</param>
        /// <returns>Lista de pagos en formato JSON</returns>
        [HttpGet("delegado/pagos-rango")]
        public IActionResult GetPagosPorDelegadoRango(
            [FromQuery] string fechaInicio,
            [FromQuery] string fechaFin,
            [FromQuery] int? cobradorId = null)
        {
            try
            {
                var resultado = _bolsaService.ObtenerPagosPorDelegadoRango(fechaInicio, fechaFin, cobradorId);
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener pagos por delegado en rango de fechas: {ex.Message}" });
            }
        }


    }
}