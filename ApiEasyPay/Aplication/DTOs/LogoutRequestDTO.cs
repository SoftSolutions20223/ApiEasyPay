namespace ApiEasyPay.Aplication.DTOs
{
    public class LogoutRequestDTO
    {
        public string Token { get; set; }
        public string TipoUsuario { get; set; } // "J" para Jefe, "C" para Cobrador
    }
}
