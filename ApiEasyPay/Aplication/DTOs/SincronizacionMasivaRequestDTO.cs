using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Aplication.DTOs
{
    /// <summary>
    /// DTO para solicitud de sincronización masiva de datos
    /// </summary>
    public class SincronizacionMasivaRequestDTO
    {
        /// <summary>
        /// Nombre de la tabla a sincronizar
        /// </summary>
        [Required(ErrorMessage = "El nombre de la tabla es obligatorio")]
        public string Tabla { get; set; }

        /// <summary>
        /// Colección de datos a sincronizar en formato JObject
        /// </summary>
        [Required(ErrorMessage = "Los datos son obligatorios")]
        public List<JObject> DatosMasivos { get; set; }

        /// <summary>
        /// Indica si se aplica modo estricto en la validación
        /// </summary>
        public bool ModoEstricto { get; set; } = true;

        /// <summary>
        /// Tamaño del lote para procesamiento por lotes
        /// </summary>
        public int TamanoLote { get; set; } = 100;

        /// <summary>
        /// Tiempo de espera en segundos
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Número máximo de reintentos
        /// </summary>
        public int MaxReintentos { get; set; } = 3;
    }
}
