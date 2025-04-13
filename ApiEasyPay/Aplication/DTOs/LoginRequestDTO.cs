namespace ApiEasyPay.Aplication.DTOs
{
    public class LoginRequestDTO
    {
        public string Usuario { get; set; }
        public string Contraseña { get; set; }
        public string CodigoRecuperacion { get; set; }
        public string Marca { get; set; }
        public string Modelo { get; set; }
        public string Sistema { get; set; }
    }
}
