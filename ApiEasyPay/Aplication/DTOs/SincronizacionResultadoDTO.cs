using Newtonsoft.Json.Linq;

namespace ApiEasyPay.Aplication.DTOs
{
    /// <summary>
    /// DTO para resultados individuales de sincronización
    /// </summary>
    public class SincronizacionResultadoDTO
    {/// <summary>
     /// Identificador de fila
     /// </summary>
        public int RowId { get; set; }

        /// <summary>
        /// Operación realizada (INSERT/UPDATE)
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Código del registro
        /// </summary>
        public decimal Cod { get; set; }

        /// <summary>
        /// Código del cobrador
        /// </summary>
        public decimal Cobrador { get; set; }

        /// <summary>
        /// Fecha y hora de la solicitud
        /// </summary>
        public DateTime RequestTimeUTC { get; set; }

        /// <summary>
        /// Datos del registro
        /// </summary>
        public JObject Record { get; set; }
    }
}
