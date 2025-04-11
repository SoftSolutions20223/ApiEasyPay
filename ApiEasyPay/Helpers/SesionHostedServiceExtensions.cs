using ApiEasyPay.Aplication.Services;

namespace ApiEasyPay.Helpers
{
    public static class SesionHostedServiceExtensions
    {
        public static IServiceCollection AddSesionHostedService(this IServiceCollection services)
        {
            services.AddHostedService<SesionHostedService>();
            return services;
        }
    }
}