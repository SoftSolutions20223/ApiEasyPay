using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class ValoresBolsa
    {
        [Range(-999999999999999999, 999999999999999999)]
        public int? Credito { get; set; }
        [MaxLength(2000)]
        public string Entregas { get; set; }
        public decimal? Valor { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Bolsa { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        [MaxLength(2000)]
        public string Gasto { get; set; }
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
