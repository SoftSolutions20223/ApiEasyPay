using System.ComponentModel.DataAnnotations;

namespace ApiEasyPay.Aplication.DTOs
{
    public class UsuarioRequestDTO
    {
        public decimal Cod { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
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

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [MaxLength(200)]
        public string Contraseña { get; set; }

        [Required(ErrorMessage = "El usuario es obligatorio")]
        [MaxLength(200)]
        public string Usuario { get; set; }

        public bool? Estado { get; set; }

        [Required(ErrorMessage = "El tipo de usuario es obligatorio")]
        [RegularExpression("[CD]", ErrorMessage = "El tipo de usuario debe ser 'C' para Cobrador o 'D' para Delegado")]
        public string TipoUsuario { get; set; }

        [Required(ErrorMessage = "El ID del jefe es obligatorio")]
        public decimal Jefe { get; set; }
    }
}
