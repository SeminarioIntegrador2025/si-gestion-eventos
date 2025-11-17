namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IEventoBusinessRules
    {
        Task<bool> IsDateRangeAvailableAsync(DateTime inicio, DateTime fin, TimeSpan horaInicio, TimeSpan horaFin, int? excludeEventoId = null);
        Task<bool> CanCancelEventoAsync(int eventoId);
        Task<bool> CanModifyEventoAsync(int eventoId);
        Task<bool> HasPaymentsAsync(int eventoId);
        Task<bool> HasFianzaAsync(int eventoId);
        Task<bool> IsClienteActiveAsync(int clienteId);
        Task<bool> IsEventoInFutureAsync(DateTime inicio);
        Task<bool> IsValidDateRangeAsync(DateTime inicio, DateTime fin, TimeSpan horaInicio, TimeSpan horaFin);
        Task<bool> IsReservationAmountValidAsync(decimal montoReserva, decimal costoAlquiler);
        Task<bool> CanRescheduleEventoAsync(int eventoId);
        Task<int> GetActiveEventsCountForClienteAsync(int clienteId);
    }
}