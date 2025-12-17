using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Common;

namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IEventoService
    {
        // Consultas
        Task<PaginatedList<EventoVM>> GetAllPaginatedAsync(string? searchQuery, DateTime? fechaDesde, DateTime? fechaHasta, EventoEstado? estado, string ordenarPor, int page, int pageSize);
        Task<EventoVM?> GetByIdAsync(int id);
        Task<List<EventoVM>> GetLatestAsync(int count);
        Task<List<EventoVM>> ObtenerTodosFiltradosAsync(string? q, DateTime? fechaDesde, DateTime? fechaHasta, EventoEstado? estado);
        // CRUD y Acciones
        Task<ServiceResult<EventoVM>> CreateAsync(EventoVM eventoVM);
        Task<ServiceResult<EventoVM>> CreateEventWithPaymentAsync(EventoVM eventoVM, PagoReservaVM pagoVM);
        Task<ServiceResult<EventoVM>> UpdateAsync(EventoVM eventoVM);
        Task<ServiceResult<bool>> CancelAsync(int id);
        Task<ServiceResult<bool>> ConfirmAsync(int id);


        // --- NUEVO MÉTODO DE REPROGRAMACIÓN ---
        Task<ServiceResult<bool>> ReprogramarAsync(ReprogramarEventoVM model);

        // ELIMINAR O COMENTAR EL MÉTODO VIEJO:
        // Task<ServiceResult<bool>> RescheduleAsync(int id, DateTime nuevaFechaInicio...);

        // Servicios de Fondo (Background)
        Task<List<EventoVM>> GetAlertasServiciosAsync();
        Task<ServiceResult<int>> MarkCompletedEventsAsync();
        Task<ServiceResult<int>> CheckAndCancelUnpaidEventsAsync();

        // Helpers y Dropdowns
        Task<IEnumerable<SelectListItem>> GetEventosSinFianzaParaDropdownAsync();
        Task<IEnumerable<SelectListItem>> GetTiposEventoParaDropdownAsync();
        Task<IEnumerable<SelectListItem>> GetEventosAdeudadosParaDropdownAsync();
        Task<bool> CanModifyEventoAsync(int eventoId);
        Task<bool> HasConflictingEventsAsync(DateTime inicio, DateTime fin, TimeSpan horaInicio, TimeSpan horaFin, int? excludeEventoId = null);
        Task<IEnumerable<SelectListItem>> GetEventosParaFiltroPagosAsync();
    }
}