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
                var (sesion, errorMsg) = await _loginService.IniciarSesionAsync(
                    request.Usuario,
                    request.Contraseña,
                    request.CodigoRecuperacion);

                if (!string.IsNullOrEmpty(errorMsg))
                    return BadRequest(new { mensaje = errorMsg });

                if (sesion == null)
                    return Unauthorized("Credenciales inválidas");

                if (sesion.Rol == "U") // Si es cobrador
                {
                    var stream = await _loginService.GenerarArchivoSincronizacionAsync(sesion.Cod);
                    return File(stream, "application/octet-stream", "sincronizacion.gz");
                }

                return Ok(sesion);
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = ex.Message });
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

        [HttpPost("generarCodigoRecuperacion")]
        public async Task<IActionResult> GenerarCodigoRecuperacion([FromBody] CodigoRecuperacionRequestDTO request)
        {
            try
            {
                var (exito, resultado) = await _loginService.GenerarCodigoRecuperacion(
                    request.CobradorId,
                    request.HorasValidez ?? 24);

                if (!exito)
                    return BadRequest(new { mensaje = resultado });

                return Ok(new { codigo = resultado });
            }
            catch (Exception ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }

        [HttpPost("cerrar")]
        public async Task<IActionResult> CerrarSesion([FromBody] LogoutRequestDTO request)
        {
            var (resultado,mensaje) = await _loginService.CerrarSesionAsync(request.Token, request.TipoUsuario);

            if (!resultado)
                return BadRequest(mensaje);

            return Ok(new { mensaje = mensaje });
        }
    }

}