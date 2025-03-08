using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class Creditos
    {
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        [Required]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime FechaInicio { get; set; }
        public decimal? PorceInteres { get; set; }
        public decimal? Total { get; set; }
        public decimal? TotalPagar { get; set; }
        public decimal? TotalPagado { get; set; }
        public decimal? ValorCuota { get; set; }
        [Range(-999999999999999999, 999999999999999999)]
        public int? NumeroDeCuotas { get; set; }
        [Range(-999999999999999999, 999999999999999999)]
        public int? CuotaActual { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cliente { get; set; }
        [Required(AllowEmptyStrings = false)]
        [MaxLength(10)]
        public string Estado { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cobrador { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaRegistro { get; set; }
        public bool? DebeTerminar { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaFin { get; set; }
        [MaxLength(1000)]
        public string Descripcion { get; set; }
        [MaxLength(10)]
        public string TipoCredito { get; set; }
        public decimal? TotalPagExtOfi { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? ProximoPago { get; set; }
        [Range(-999999999999999999, 999999999999999999)]
        public int? FrecuenciaPago { get; set; }
        [Range(-999999999999999999, 999999999999999999)]
        public int? OrdenRuta { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? Amortizacion { get; set; }
        public decimal? SaldoCuotas { get; set; }
    }
}
