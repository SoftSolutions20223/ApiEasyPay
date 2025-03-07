using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Aplication.DTOs
{
    /// <summary>
    /// DTO para solicitud de sincronización de datos individual
    /// </summary>
    public class SincronizacionRequestDTO
    { /// <summary>
      /// Nombre de la tabla a sincronizar
      /// </summary>
        [Required(ErrorMessage = "El nombre de la tabla es obligatorio")]
        public string Tabla { get; set; }

        /// <summary>
        /// Datos a sincronizar en formato JObject
        /// </summary>
        [Required(ErrorMessage = "Los datos son obligatorios")]
        public JObject Datos { get; set; }

        /// <summary>
        /// Indica si se aplica modo estricto en la validación
        /// </summary>
        public bool ModoEstricto { get; set; } = true;
    }
}
