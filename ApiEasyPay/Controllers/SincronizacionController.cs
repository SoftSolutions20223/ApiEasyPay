using ApiEasyPay.Aplication.DTOs;
using ApiEasyPay.Aplication.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace ApiEasyPay.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SincronizacionController : ControllerBase
    {
        private readonly SincronizacionService _sincronizacionService;

        public SincronizacionController(SincronizacionService sincronizacionService)
        {
            _sincronizacionService = sincronizacionService;
        }

        /// <summary>
        /// Sincroniza un registro individual con la base de datos
        /// </summary>
        /// <param name="request">Datos a sincronizar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("sincronizar")]
        public async Task<IActionResult> SincronizarDatos([FromBody] SincronizacionRequestDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message, data) = await _sincronizacionService.SincronizarDatosAsync(request);

            if (!success)
                return BadRequest(new { mensaje = message });

            // Devolvemos la respuesta tal como viene del procedimiento almacenado
            return Content(data, "application/json");
        }

        /// <summary>
        /// Sincroniza múltiples registros con la base de datos
        /// </summary>
        /// <param name="request">Colección de datos a sincronizar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("sincronizar-masivo")]
        public async Task<IActionResult> SincronizarDatosMasivo([FromBody] SincronizacionMasivaRequestDTO request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message, data) = await _sincronizacionService.SincronizarDatosMasivoAsync(request);

            if (!success)
                return BadRequest(new { mensaje = message });

            // Devolvemos la respuesta tal como viene del procedimiento almacenado
            return Content(data, "application/json");
        }

        /// <summary>
        /// Sincroniza datos desde un archivo JSON
        /// </summary>
        /// <param name="tabla">Nombre de la tabla donde sincronizar</param>
        /// <param name="modoEstricto">Indica si se aplica validación estricta</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("sincronizar-archivo")]
        public async Task<IActionResult> SincronizarArchivo([FromQuery] string tabla, [FromQuery] bool modoEstricto = true)
        {
            if (string.IsNullOrEmpty(tabla))
            {
                return BadRequest("Debe especificar la tabla destino");
            }

            // Verificar si hay archivos en la solicitud
            if (Request.Form.Files.Count == 0)
            {
                return BadRequest("No se ha recibido ningún archivo para sincronizar");
            }

            try
            {
                // Obtener el primer archivo
                var archivo = Request.Form.Files[0];

                // Leer el contenido del archivo
                using var reader = new StreamReader(archivo.OpenReadStream());
                string jsonContent = await reader.ReadToEndAsync();

                // Intentar parsear el contenido como JSON
                if (!TryParseJson(jsonContent, out JToken jsonData))
                {
                    return BadRequest("El archivo no contiene un JSON válido");
                }

                // Determinar si es un objeto individual o un array
                if (jsonData is JObject jobject)
                {
                    // Si es un objeto, usar el método de sincronización individual
                    var request = new SincronizacionRequestDTO
                    {
                        Tabla = tabla,
                        Datos = jobject,
                        ModoEstricto = modoEstricto
                    };

                    var (success, message, data) = await _sincronizacionService.SincronizarDatosAsync(request);

                    if (!success)
                        return BadRequest(new { mensaje = message });

                    return Content(data, "application/json");
                }
                else if (jsonData is JArray jarray)
                {
                    // Si es un array, usar el método de sincronización masiva
                    var request = new SincronizacionMasivaRequestDTO
                    {
                        Tabla = tabla,
                        DatosMasivos = jarray,
                        ModoEstricto = modoEstricto
                    };

                    var (success, message, data) = await _sincronizacionService.SincronizarDatosMasivoAsync(request);

                    if (!success)
                        return BadRequest(new { mensaje = message });

                    return Content(data, "application/json");
                }
                else
                {
                    return BadRequest("El formato JSON no es reconocido");
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error al procesar el archivo: {ex.Message}");
            }
        }

        /// <summary>
        /// Intenta parsear una cadena como JSON
        /// </summary>
        private bool TryParseJson(string jsonString, out JToken jsonData)
        {
            jsonData = null;
            try
            {
                jsonData = JToken.Parse(jsonString);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}