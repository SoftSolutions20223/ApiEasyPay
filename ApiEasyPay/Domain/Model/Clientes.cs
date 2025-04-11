using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class Clientes
    {
        [MaxLength(200)]
        public string Nombres { get; set; }
        [MaxLength(200)]
        public string Apellidos { get; set; }
        [MaxLength(50)]
        public string Telefono { get; set; }
        [Required(AllowEmptyStrings = false)]
        [MaxLength(200)]
        public string Documento { get; set; }
        [MaxLength(200)]
        public string Direccion { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cod { get; set; }
        public bool? Estado { get; set; }
        [MaxLength(200)]
        public string Correo { get; set; }
        public short? Estatus { get; set; }
        [Required]
        [Range(-999999999999999999, 999999999999999999)]
        public int Cobrador { get; set; }
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? FechaActualizacion { get; set; }
        public string Lat { get; set; }
        public string Long { get; set; }
    }
}