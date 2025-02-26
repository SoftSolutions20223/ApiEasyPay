using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Aplication.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ApiEasyPay.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly LoginService _loginService;

        public LoginController(LoginService loginService)
        {
            _loginService = loginService;
        }

        [HttpPost("iniciar")]
        public async Task<IActionResult> IniciarSesion([FromBody] LoginRequestDTO request)
        {
            try
            {
                var sesion = await _loginService.IniciarSesionAsync(
                    request.Usuario,
                    request.Contraseña,
                    request.CodigoRecuperacion);

                if (sesion == null)
                    return Unauthorized("Credenciales inválidas");

                if (sesion.Rol == "U") // Si es cobrador
                {
                    var stream = await _loginService.GenerarArchivoSincronizacionAsync(sesion.Cod);
                    return File(stream, "application/octet-stream", "sincronizacion.gz");
                }

                // Para jefes solo devolvemos el token
                return Ok(sesion);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("estado")]
        public async Task<IActionResult> VerificarEstadoSesion([FromQuery] string usuario = null, [FromQuery] string token = null)
        {
            // Si se proporciona token, validamos la sesión existente
            if (!string.IsNullOrEmpty(token))
            {
                var sesion = await _loginService.ValidateSessionAsync(token);
                if (sesion == null)
                    return Unauthorized("Sesión inválida o expirada");
                return Ok(sesion);
            }

            // Si se proporciona usuario, verificamos su estado
            if (!string.IsNullOrEmpty(usuario))
            {
                var estadoSesion = await _loginService.VerificarEstadoSesionAsync(usuario);
                return Ok(estadoSesion);
            }

            return BadRequest("Debe proporcionar un token o un nombre de usuario");
        }

        [HttpPost("cerrar")]
        public async Task<IActionResult> CerrarSesion([FromBody] LogoutRequestDTO request)
        {
            var resultado = await _loginService.CerrarSesionAsync(request.Token, request.TipoUsuario);

            if (!resultado)
                return BadRequest("No se pudo cerrar la sesión");

            return Ok(new { mensaje = "Sesión cerrada correctamente" });
        }
    }

}