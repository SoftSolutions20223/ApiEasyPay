using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Domain.Model
{
    public class Clientes
    {
        [MaxLength(200)]
        public string Nombres { get; set; }
        [MaxLength(200)]
        public string Apellidos { get; set; }
        [MaxLength(20)]
        public string Telefono { get; set; }
        [Required]
        [MaxLength(200)]
        public string Documento { get; set; }
        [MaxLength(200)]
        public string Direccion { get; set; }
        [MaxLength(200)]
        public string Contraseña { get; set; }
        [Range(-999999999999999999, 999999999999999999)]
        public decimal Cod { get; set; }
        [MaxLength(200)]
        public string Usuario { get; set; }
        public bool? Estado { get; set; }
        [MaxLength(200)]
        public string Correo { get; set; }
        public short? Estatus { get; set; }
        [Range(-999999999999999999, 999999999999999999)]
        public decimal Cobrador { get; set; }
        public DateTime? FechaActualizacion { get; set; }
    }
}