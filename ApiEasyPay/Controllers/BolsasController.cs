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
        /// Obtiene un resumen de las bolsas abiertas del jefe actual
        /// </summary>
        /// <returns>Lista de bolsas abiertas en formato JSON</returns>
        [HttpGet("abiertas")]
        public IActionResult GetBolsasAbiertas()
        {
            try
            {
                var resultado = _bolsaService.ObtenerBolsasAbiertas();
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
        /// Obtiene un resumen de las bolsas cerradas del jefe actual
        /// </summary>
        /// <returns>Lista de bolsas cerradas en formato JSON</returns>
        [HttpGet("cerradas")]
        public IActionResult GetBolsasCerradas()
        {
            try
            {
                var resultado = _bolsaService.ObtenerBolsasCerradas();
                return new JsonResult(resultado);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { mensaje = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener bolsas cerradas: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene datos resumidos de bolsa para la fecha indicada
        /// </summary>
        /// <param name="fecha">Fecha en formato yyyy-MM-dd</param>
        /// <returns>Datos de la bolsa en formato JSON</returns>
        [HttpGet("datos")]
        public IActionResult GetDatosBolsa([FromQuery] string fecha)
        {
            try
            {
                if (string.IsNullOrEmpty(fecha))
                {
                    fecha = DateTime.Now.ToString("yyyy-MM-dd");
                }

                var resultado = _bolsaService.ObtenerDatosBolsa(fecha);
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
        /// Obtiene las entregas de una bolsa específica
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <returns>Lista de entregas en formato JSON</returns>
        [HttpGet("{codBolsa}/entregas")]
        public IActionResult GetEntregasBolsa(int codBolsa)
        {
            try
            {
                var resultado = _bolsaService.ObtenerEntregasBolsa(codBolsa);
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
        /// Obtiene los gastos de una bolsa específica
        /// </summary>
        /// <param name="codBolsa">Código de la bolsa</param>
        /// <returns>Lista de gastos en formato JSON</returns>
        [HttpGet("{codBolsa}/gastos")]
        public IActionResult GetGastosBolsa(int codBolsa)
        {
            try
            {
                var resultado = _bolsaService.ObtenerGastosBolsa(codBolsa);
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
    }
}