namespace ApiEasyPay.Aplication.DTOs
{
    public class SesionStatusDTO
    {
        public bool Existe { get; set; }
        public bool UsuarioExiste { get; set; }
        public int Cod { get; set; }
        public bool SesionActiva { get; set; }
        public string TipoUsuario { get; set; }
        public bool RequiereCodigoRecuperacion { get; set; }
    }
}
