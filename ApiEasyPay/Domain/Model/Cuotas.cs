using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class Cuotas
    {
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? Fecha { get; set; }
        public decimal? Valor { get; set; }
        public decimal? ValorPagado { get; set; }
        public decimal? Debe { get; set; }
        [MaxLength(100)]
        public string NomCuota { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int NumCuota { get; set; }
        [MaxLength(10)]
        public string Estado { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Credito { get; set; }
        public bool? Visitado { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cobrador { get; set; }
    }
}
