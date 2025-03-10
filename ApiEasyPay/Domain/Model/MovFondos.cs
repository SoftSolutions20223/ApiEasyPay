using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class MovFondos
    {
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int FondoInversion { get; set; }
        public decimal? Valor { get; set; }
        public bool? Tipo { get; set; }
        [MaxLength(1000)]
        public string Descripcion { get; set; }
        [Required]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime Fecha { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaActualizacion { get; set; }
    }
}
