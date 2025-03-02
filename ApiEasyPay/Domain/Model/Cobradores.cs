using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class Cobradores
    {
        [MaxLength(200)]
        public string Nombres { get; set; }
        [MaxLength(200)]
        public string Apellidos { get; set; }
        [MaxLength(20)]
        public string Telefono { get; set; }
        [MaxLength(20)]
        public string Documento { get; set; }
        [MaxLength(20)]
        public string Direccion { get; set; }
        [MaxLength(200)]
        public string Contraseña { get; set; }
        [Range(-999999999999999999, 999999999999999999)]
        public decimal Cod { get; set; }
        [MaxLength(200)]
        public string Usuario { get; set; }
        public bool? Estado { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public decimal Jefe { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaActualizacion { get; set; }
    }
}