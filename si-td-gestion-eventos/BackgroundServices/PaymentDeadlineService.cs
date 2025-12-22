using si_td_gestion_eventos.Services.Contracts;

namespace si_td_gestion_eventos.BackgroundServices
{
    public class PaymentDeadlineService : BackgroundService
    {
        private readonly ILogger<PaymentDeadlineService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public PaymentDeadlineService(ILogger<PaymentDeadlineService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Servicio de Plazo de Pago iniciado.");

            // Timer configurado a 1 minuto (según tu código original)
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Servicio de Plazo de Pago detenido.");
                    return;
                }

                _logger.LogInformation("Ejecutando mantenimiento automático de estados de eventos...");

                try
                {
                    // Crear scope para poder invocar al IEventoService (Scoped) dentro del BackgroundService (Singleton)
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        var eventoService = scope.ServiceProvider.GetRequiredService<IEventoService>();

                        // --- CORRECCIÓN AQUÍ ---
                        // Reemplazamos los métodos viejos por el método unificado
                        var result = await eventoService.ActualizarEstadosEventosPasadosAsync();

                        if (result.Success && result.Data > 0)
                        {
                            // El log refleja que se actualizaron N eventos (ya sea a Realizado o a PendienteAdeudado)
                            _logger.LogInformation($"Mantenimiento Automático: Se actualizaron {result.Data} eventos pasados correctamente.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ocurrió un error durante el chequeo de plazos de pago y estados.");
                }
            }
        }
    }
}