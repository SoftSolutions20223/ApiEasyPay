namespace ApiEasyPay.Seguridad.Helpers
{
    public class ConfigurationOptions
    {
        private readonly IConfiguration _configuracion;
        private static ConfigurationOptions _instance;
        public string StrConexBdSql { get; set; }
        public string StrConexBdMongo { get; set; }
        public string DatabaseNameMongo { get; set; }

        // Constructor privado para evitar instanciación externa
        private ConfigurationOptions(IConfiguration configuration)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            StrConexBdSql = env == "Development"
                ? configuration["ConfigurationOptions:StrConexBd_Principal_Development"]
                : configuration["ConfigurationOptions:StrConexBd_Principal_Production"];
            StrConexBdMongo = configuration["ConfigurationOptions:StrConexBd_MongoDB"];
            DatabaseNameMongo = configuration["ConfigurationOptions:DatabaseName_Mongo"];

        }

        // Método para inicializar la instancia. Debe llamarse una vez al inicio.
        public static void Initialize(IConfiguration configuration)
        {
            if (_instance == null)
            {
                _instance = new ConfigurationOptions(configuration);
            }
        }

        // Propiedad para acceder a la instancia
        public static ConfigurationOptions Instance
        {
            get
            {
                if (_instance == null)
                    throw new InvalidOperationException("ConfigurationOptions no ha sido inicializado. Llama a Initialize() primero.");
                return _instance;
            }
        }
    }
}