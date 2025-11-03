using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Common;

namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IEventoService
    {
        Task<PaginatedList<EventoVM>> GetAllPaginatedAsync(
            string? searchQuery,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            EventoEstado? estado,
            string ordenarPor,
            int page,
            int pageSize);
        Task<List<EventoVM>> GetLatestAsync(int count);
        Task<EventoVM?> GetByIdAsync(int id);
        Task<ServiceResult<EventoVM>> CreateAsync(EventoVM eventoVM);
        Task<ServiceResult<EventoVM>> UpdateAsync(EventoVM eventoVM);
        Task<ServiceResult<bool>> CancelAsync(int id);
        Task<ServiceResult<bool>> RescheduleAsync(int id, DateTime nuevaFechaInicio, DateTime nuevaFechaFin, TimeSpan nuevaHoraInicio, TimeSpan nuevaHoraFin);
        Task<ServiceResult<bool>> ConfirmAsync(int id);
        Task<IEnumerable<SelectListItem>> GetTiposEventoParaDropdownAsync();
        Task<bool> CanModifyEventoAsync(int eventoId);
        Task<bool> HasConflictingEventsAsync(DateTime inicio, DateTime fin, TimeSpan horaInicio, TimeSpan horaFin, int? excludeEventoId = null);
        Task<IEnumerable<SelectListItem>> GetEventosAdeudadosParaDropdownAsync();
        Task<ServiceResult<EventoVM>> CreateEventWithPaymentAsync(EventoVM eventoVM, PagoReservaVM pagoVM);
        Task<ServiceResult<int>> CheckAndCancelUnpaidEventsAsync();
    }
}