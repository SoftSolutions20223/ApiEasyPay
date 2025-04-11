using ApiEasyPay.Aplication.Services;

namespace ApiEasyPay.Helpers
{
    public class SesionHostedService : IHostedService
    {
        private readonly SesionInicializadorService _sesionService;
        private readonly ILogger<SesionHostedService> _logger;

        public SesionHostedService(
            SesionInicializadorService sesionService,
            ILogger<SesionHostedService> logger)
        {
            _sesionService = sesionService;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Iniciando servicio de sincronización de sesiones...");

            try
            {
                await _sesionService.SincronizarSesionesActivas();
                _logger.LogInformation("Servicio de sincronización de sesiones inicializado correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al iniciar el servicio de sincronización de sesiones: {Message}", ex.Message);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Servicio de sincronización de sesiones detenido");
            return Task.CompletedTask;
        }
    }
}