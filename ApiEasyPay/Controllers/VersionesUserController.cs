using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Aplication.Services;
using ApiEasyPay.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiEasyPay.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [TokenAuthorization]
    public class VersionesUserController : ControllerBase
    {
        private readonly VersionAppService _versionAppService;

        public VersionesUserController(VersionAppService versionAppService)
        {
            _versionAppService = versionAppService;
        }

        /// <summary>
        /// Obtiene la versión de la aplicación del usuario autenticado
        /// </summary>
        /// <returns>Datos de versión en formato JSON</returns>
        [HttpGet]
        public async Task<IActionResult> ObtenerVersionApp()
        {
            try
            {
                var (success, message, data) = await _versionAppService.ObtenerVersionAppAsync();

                if (!success)
                    return BadRequest(new { mensaje = message });

                return new JsonResult(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al obtener versión: {ex.Message}" });
            }
        }

        /// <summary>
        /// Actualiza la versión de la aplicación del usuario autenticado
        /// </summary>
        /// <param name="request">Datos con la nueva versión</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost]
        public async Task<IActionResult> ActualizarVersionApp([FromBody] VersionAppRequestDTO request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var (success, message, data) = await _versionAppService.ActualizarVersionAppAsync(request);

                if (!success)
                    return BadRequest(new { mensaje = message });

                return new JsonResult(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al actualizar versión: {ex.Message}" });
            }
        }

        /// <summary>
        /// Actualiza la versión de la aplicación utilizando un header personalizado
        /// </summary>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("header")]
        public async Task<IActionResult> ActualizarVersionAppDesdeHeader()
        {
            try
            {
                // Obtener la versión del header
                if (!Request.Headers.TryGetValue("X-App-Version", out var versionHeader))
                {
                    return BadRequest(new { mensaje = "No se proporcionó el header X-App-Version" });
                }

                var request = new VersionAppRequestDTO
                {
                    VersionApp = versionHeader.ToString()
                };

                var (success, message, data) = await _versionAppService.ActualizarVersionAppAsync(request);

                if (!success)
                    return BadRequest(new { mensaje = message });

                return new JsonResult(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = $"Error al actualizar versión: {ex.Message}" });
            }
        }
    }
}