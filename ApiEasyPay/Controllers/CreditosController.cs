using ApiEasyPay.Aplication.Services;
using ApiEasyPay.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiEasyPay.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [TokenAuthorization]
    public class CreditosController : ControllerBase
    {
        private readonly CreditosService _creditosService;

        public CreditosController(CreditosService creditosService)
        {
            _creditosService = creditosService;
        }

        /// <summary>
        /// Obtiene un resumen estadístico de créditos para el jefe actual
        /// </summary>
        /// <returns>Estadísticas de créditos en formato JSON</returns>
        [HttpGet("resumen-jefe")]
        public IActionResult GetResumenCreditosJefe()
        {
            try
            {
                var resultado = _creditosService.ObtenerResumenCreditosJefe();
                return new JsonResult(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener resumen de créditos: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de créditos agrupadas por cobrador
        /// </summary>
        /// <returns>Estadísticas por cobrador en formato JSON</returns>
        [HttpGet("resumen-por-cobrador")]
        public IActionResult GetResumenPorCobrador()
        {
            try
            {
                var resultado = _creditosService.ObtenerResumenPorCobrador();
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener resumen por cobrador: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene la lista de créditos vigentes para un cobrador específico
        /// </summary>
        /// <param name="cobradorId">ID del cobrador</param>
        /// <returns>Lista de créditos vigentes en formato JSON</returns>
        [HttpGet("vigentes/{cobradorId}")]
        public IActionResult GetCreditosVigentes(int cobradorId)
        {
            try
            {
                var resultado = _creditosService.ObtenerCreditosVigentes(cobradorId);
                return new JsonResult(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener créditos vigentes: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene la lista de créditos terminados para un cobrador específico
        /// </summary>
        /// <param name="cobradorId">ID del cobrador</param>
        /// <returns>Lista de créditos terminados en formato JSON</returns>
        [HttpGet("terminados/{cobradorId}")]
        public IActionResult GetCreditosTerminados(int cobradorId)
        {
            try
            {
                var resultado = _creditosService.ObtenerCreditosTerminados(cobradorId);
                return new JsonResult(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener créditos terminados: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene detalles de un crédito específico para un cobrador
        /// </summary>
        /// <param name="cobradorId">Id del cobrador</param>
        /// <param name="creditoId">Id del crédito</param>
        /// <returns>Detalles del crédito en formato JSON</returns>
        [HttpGet("{cobradorId}/credito/{creditoId}")]
        public IActionResult GetCreditoDetalle(int cobradorId, int creditoId)
        {
            try
            {
                var resultado = _creditosService.ObtenerDetalleCredito(creditoId, cobradorId);
                if (resultado == null)
                    return NotFound(new { mensaje = "Crédito no encontrado" });

                return new JsonResult(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener detalles del crédito: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene las cuotas de un crédito específico para un cobrador
        /// </summary>
        /// <param name="cobradorId">Id del cobrador</param>
        /// <param name="creditoId">Id del crédito</param>
        /// <returns>Lista de cuotas en formato JSON</returns>
        [HttpGet("{cobradorId}/credito/{creditoId}/cuotas")]
        public IActionResult GetCuotasCredito(int cobradorId, int creditoId)
        {
            try
            {
                var resultado = _creditosService.ObtenerCuotasCredito(creditoId, cobradorId);
                if (resultado == null || !resultado.HasValues)
                    return NotFound(new { mensaje = "No se encontraron cuotas para este crédito" });

                return new JsonResult(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener cuotas del crédito: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene el historial de pagos de un crédito específico para un cobrador
        /// </summary>
        /// <param name="cobradorId">Id del cobrador</param>
        /// <param name="creditoId">Id del crédito</param>
        /// <returns>Historial de pagos en formato JSON</returns>
        [HttpGet("{cobradorId}/credito/{creditoId}/historial")]
        public IActionResult GetHistorialCredito(int cobradorId, int creditoId)
        {
            try
            {
                var resultado = _creditosService.ObtenerHistorialCredito(creditoId, cobradorId);
                if (resultado == null || !resultado.HasValues)
                    return NotFound(new { mensaje = "No se encontró historial para este crédito" });

                return new JsonResult(resultado);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener historial del crédito: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene un resumen estadístico de créditos para el delegado actual
        /// </summary>
        /// <returns>Estadísticas de créditos en formato JSON</returns>
        [HttpGet("resumen-delegado")]
        public IActionResult GetResumenCreditosDelegado()
        {
            try
            {
                var resultado = _creditosService.ObtenerResumenCreditosDelegado();
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener resumen de créditos: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de créditos agrupadas por cobrador asignado al delegado
        /// </summary>
        /// <returns>Estadísticas por cobrador en formato JSON</returns>
        [HttpGet("resumen-por-cobrador-delegado")]
        public IActionResult GetResumenPorCobradorDelegado()
        {
            try
            {
                var resultado = _creditosService.ObtenerResumenPorCobradorDelegado();
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener resumen por cobrador: {ex.Message}" });
            }
        }
    }
}