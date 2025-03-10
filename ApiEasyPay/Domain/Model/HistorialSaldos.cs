using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class HistorialSaldos
    {
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        [MaxLength(2000)]
        public string Descripcion { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cobrador { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? Fecha { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaActualizacion { get; set; }
    }
}
