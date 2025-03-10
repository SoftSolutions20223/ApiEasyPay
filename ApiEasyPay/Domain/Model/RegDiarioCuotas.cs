using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class RegDiarioCuotas
    {
        [Required]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime Fecha { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Credito { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        public bool? Visitado { get; set; }
        public decimal? Valor { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Bolsa { get; set; }
        [Required(AllowEmptyStrings = false)]
        [MaxLength(50)]
        public string Descripcion { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cobrador { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaActualizacion { get; set; }
    }
}
