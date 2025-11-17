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

          
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1)); //DESPUES CAMBIARLO A TIEMPO MAS RAZONABLE 
          
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Servicio de Plazo de Pago detenido.");
                    return;
                }

                _logger.LogInformation("Ejecutando chequeo de plazos de pago y finalizacion de eventos");

                try
                {
                   
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {                  
                        var eventoService = scope.ServiceProvider.GetRequiredService<IEventoService>();
                     
                        var result = await eventoService.CheckAndCancelUnpaidEventsAsync();

                        if (result.Success && result.Data > 0)
                        {
                            _logger.LogInformation($"Se cancelaron automáticamente {result.Data} eventos por falta de pago.");
                        }

                        var completeResult = await eventoService.MarkCompletedEventsAsync();

                        if (completeResult.Success && completeResult.Data > 0)
                        {
                            _logger.LogInformation($"Se marcaron automáticamente {completeResult.Data} eventos como Realizados.");
                        }

                    }
                }
                catch (Exception ex)
                {                   
                    _logger.LogError(ex, "Ocurrió un error durante el chequeo de plazos de pago.");
                }
            }
        }
    }
}