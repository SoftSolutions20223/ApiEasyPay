using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Databases.Connections;
using ApiEasyPay.Seguridad.Helpers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ApiEasyPay.Helpers
{
    public class TokenAuthorizationAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var request = context.HttpContext.Request;
            var authorizationHeader = request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
            {
                context.Result = new UnauthorizedObjectResult(new { mensaje = "Token no proporcionado" });
                return;
            }

            var token = authorizationHeader.Substring("Bearer ".Length).Trim();

            // Obtener servicios necesarios
            var conexionMongo = context.HttpContext.RequestServices.GetRequiredService<ConexionMongo>();
            var conexionSql = context.HttpContext.RequestServices.GetRequiredService<ConexionSql>();

            // Validar token en MongoDB
            var sessionData = await conexionMongo.GetSessionByTokenAsync(token);
            if (sessionData == null)
            {
                context.Result = new UnauthorizedObjectResult(new { mensaje = "Sesión inválida o expirada" });
                return;
            }

            // Configurar conexión del cliente
            var sesion = sessionData.ToObject<SesionDTO>();
            conexionSql.BdPrincipal = ConfigurationOptions.Instance.StrConexBdSql;
            conexionSql.BdCliente = conexionSql.CreaCadenaConexServ(
                sesion.HostBd,
                sesion.NameBd,
                sesion.UsuBd,
                sesion.PwBd
            );

            // Almacenar información del usuario en el contexto
            context.HttpContext.Items["Usuario"] = sesion.Usuario;
            context.HttpContext.Items["Rol"] = sesion.Rol;
            context.HttpContext.Items["SesionData"] = sessionData;

            await next();
        }
    }
}
