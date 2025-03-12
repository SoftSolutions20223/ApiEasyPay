using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Aplication.Services;
using ApiEasyPay.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace ApiEasyPay.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [TokenAuthorization]
    public class UsuariosController : ControllerBase
    {
        private readonly UsuariosService _usuariosService;

        public UsuariosController(UsuariosService usuariosService)
        {
            _usuariosService = usuariosService;
        }

        /// <summary>
        /// Crea un nuevo usuario (cobrador o delegado)
        /// </summary>
        /// <param name="request">Datos del usuario a crear</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("crear")]
        public async Task<IActionResult> CrearUsuario([FromBody] UsuarioRequestDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message, data) = await _usuariosService.CrearUsuarioAsync(request);

            if (!success)
                return BadRequest(new { mensaje = message });

            // Retornar directamente el objeto JSON en lugar de convertirlo a string
            return new JsonResult(data);
        }

        /// <summary>
        /// Modifica un usuario existente (cobrador o delegado)
        /// </summary>
        /// <param name="request">Datos del usuario a modificar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPut("modificar")]
        public async Task<IActionResult> ModificarUsuario([FromBody] UsuarioRequestDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message, data) = await _usuariosService.ModificarUsuarioAsync(request);

            if (!success)
                return BadRequest(new { mensaje = message });

            // Retornar directamente el objeto JSON en lugar de convertirlo a string
            return new JsonResult(data);
        }

        /// <summary>
        /// Obtiene los datos de un usuario específico o de todos los usuarios
        /// </summary>
        /// <param name="codigo">Código del usuario o "*" para todos</param>
        /// <param name="tipo">Tipo de usuario: D para Delegado, C para Cobrador</param>
        /// <returns>Datos del usuario o lista de usuarios</returns>
        [HttpGet("{codigo}")]
        public async Task<IActionResult> ObtenerUsuario(string codigo, [FromQuery] string tipo = "C")
        {
            var (success, message, data) = await _usuariosService.ObtenerUsuarioAsync(codigo, tipo);

            if (!success)
                return NotFound(new { mensaje = message });

            // Retornar directamente el arreglo JSON en lugar de convertirlo a string
            return new JsonResult(data);
        }

        /// <summary>
        /// Asigna un cobrador a un delegado específico
        /// </summary>
        /// <param name="delegadoId">ID del delegado</param>
        /// <param name="cobradorId">ID del cobrador a asignar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("delegados/{delegadoId}/asignar-cobrador/{cobradorId}")]
        public async Task<IActionResult> AsignarCobradorADelegado(int delegadoId, int cobradorId)
        {
            var (success, message, data) = await _usuariosService.AsignarCobradorADelegado(delegadoId, cobradorId);

            if (!success)
                return BadRequest(new { mensaje = message });

            return new JsonResult(data);
        }

        /// <summary>
        /// Quita un cobrador de un delegado específico
        /// </summary>
        /// <param name="delegadoId">ID del delegado</param>
        /// <param name="cobradorId">ID del cobrador a quitar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpDelete("delegados/{delegadoId}/quitar-cobrador/{cobradorId}")]
        public async Task<IActionResult> QuitarCobradorDeDelegado(int delegadoId, int cobradorId)
        {
            var (success, message, data) = await _usuariosService.QuitarCobradorDeDelegado(delegadoId, cobradorId);

            if (!success)
                return BadRequest(new { mensaje = message });

            return Ok(new { mensaje = message });
        }

        /// <summary>
        /// Obtiene la lista de cobradores asignados a un delegado específico
        /// </summary>
        /// <param name="delegadoId">ID del delegado</param>
        /// <returns>Lista de cobradores asignados al delegado</returns>
        [HttpGet("delegados/{delegadoId}/cobradores")]
        public async Task<IActionResult> ObtenerCobradoresDeDelegado(int delegadoId)
        {
            var (success, message, data) = await _usuariosService.ObtenerCobradoresDeDelegado(delegadoId);

            if (!success)
                return BadRequest(new { mensaje = message });

            return new JsonResult(data);
        }

        /// <summary>
        /// Elimina un delegado específico y todas sus asignaciones
        /// </summary>
        /// <param name="delegadoId">ID del delegado a eliminar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpDelete("delegados/{delegadoId}")]
        public async Task<IActionResult> EliminarDelegado(int delegadoId)
        {
            var (success, message) = await _usuariosService.EliminarDelegado(delegadoId);

            if (!success)
                return BadRequest(new { mensaje = message });

            return Ok(new { mensaje = message });
        }

        /// <summary>
        /// Elimina un cobrador específico y todos sus registros relacionados
        /// </summary>
        /// <param name="cobradorId">ID del cobrador a eliminar</param>
        /// <param name="request">Objeto con la contraseña del administrador</param>
        /// <returns>Resultado de la operación</returns>
        [HttpDelete("cobradores/{cobradorId}")]
        public async Task<IActionResult> EliminarCobrador(int cobradorId, [FromBody] DeleteCobradorRequest request)
        {
            // Obtener el token de la sesión actual
            var authorizationHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
            {
                return Unauthorized(new { mensaje = "Token no proporcionado" });
            }
            var token = authorizationHeader.Substring("Bearer ".Length).Trim();

            var (success, message) = await _usuariosService.EliminarCobrador(cobradorId, request.AdminPassword, token);

            if (!success)
                return BadRequest(new { mensaje = message });

            return Ok(new { mensaje = message });
        }
    }
}