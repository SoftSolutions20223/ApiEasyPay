using Newtonsoft.Json.Linq;

namespace ApiEasyPay.Aplication.DTOs
{
    /// <summary>
    /// DTO para errores individuales de sincronización
    /// </summary>
    public class SincronizacionErrorDTO
    {
        /// <summary>
        /// Identificador de fila
        /// </summary>
        public int RowId { get; set; }

        /// <summary>
        /// Código de error
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// Campo que generó el error
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// Mensaje de error
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Datos originales que causaron el error
        /// </summary>
        public JObject OriginalData { get; set; }
    }
}
