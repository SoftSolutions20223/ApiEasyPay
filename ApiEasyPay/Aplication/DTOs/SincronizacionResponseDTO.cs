namespace ApiEasyPay.Aplication.DTOs
{
    /// <summary>
    /// DTO para respuesta de sincronización con éxito
    /// </summary>

    public class SincronizacionResponseDTO
    {/// <summary>
     /// Identificador único de la solicitud
     /// </summary>
        public string RequestId { get; set; }

        /// <summary>
        /// Tiempo de ejecución en milisegundos
        /// </summary>
        public int ExecutionTimeMs { get; set; }

        /// <summary>
        /// Total de registros procesados
        /// </summary>
        public int TotalRecords { get; set; }

        /// <summary>
        /// Cantidad de registros procesados correctamente
        /// </summary>
        public int ProcessedCount { get; set; }

        /// <summary>
        /// Cantidad de errores
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Cantidad de inserciones
        /// </summary>
        public int InsertCount { get; set; }

        /// <summary>
        /// Cantidad de actualizaciones
        /// </summary>
        public int UpdateCount { get; set; }

        /// <summary>
        /// Resultados detallados
        /// </summary>
        public List<SincronizacionResultadoDTO> Results { get; set; }

        /// <summary>
        /// Errores detallados
        /// </summary>
        public List<SincronizacionErrorDTO> Errors { get; set; }

        /// <summary>
        /// Indica si los resultados están truncados
        /// </summary>
        public bool ResultsTruncated { get; set; }
    }
}
