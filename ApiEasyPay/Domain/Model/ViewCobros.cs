using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class ViewCobros
    {
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Credito { get; set; }
        public decimal? ValorPagadoCuotas { get; set; }
        public decimal? ValorPagadoDeduda { get; set; }
        public decimal? ValorPagadoExtra { get; set; }
        public bool? Visitado { get; set; }
        [Required]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime Fecha { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cobrador { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaActualizacion { get; set; }
    }
}
