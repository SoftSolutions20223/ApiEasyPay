using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class Statuss
    {
        [Range(-999999999999999999, 999999999999999999)]
        public int? Puntuacion { get; set; }
        [Range(-999999999999999999, 999999999999999999)]
        public int? Credito { get; set; }
        [Range(-999999999999999999, 999999999999999999)]
        public int? Cliente { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaActualizacion { get; set; }
    }
}
