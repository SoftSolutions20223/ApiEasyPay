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
            return Ok(data);
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
            return Ok(data);
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
    }
}