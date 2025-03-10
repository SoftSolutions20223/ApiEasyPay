using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class FondoInversion
    {
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        public decimal? Monto { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Jefe { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaActualizacion { get; set; }
    }
}
