using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class Amortizaciones
    {
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? Fecha { get; set; }
        public decimal? ValorCuota { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Credito { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Bolsa { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaCuota { get; set; }
        public decimal? TotalPagar { get; set; }
        [Range(-999999999999999999, 999999999999999999)]
        public int? NumCuotas { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cobrador { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaActualizacion { get; set; }
    }
}
