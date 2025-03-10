using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class Bolsa
    {
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        public decimal? SaldoActual { get; set; }
        [MaxLength(10)]
        public string Estado { get; set; }
        [Required]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime FechaInicio { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaFin { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cobrador { get; set; }
        public decimal? TotalCobrado { get; set; }
        public decimal? TotalGastos { get; set; }
        public decimal? TotalPrestado { get; set; }
        public decimal? TotalEntregas { get; set; }
        public decimal? TotalCobradoCUO { get; set; }
        public decimal? TotalCobradoEXT { get; set; }
        public decimal? TotalCobradoDEU { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaActualizacion { get; set; }
    }
}
