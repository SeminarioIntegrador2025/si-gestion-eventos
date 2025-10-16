using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Common;

namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IEventoService
    {
        Task<PaginatedList<EventoVM>> GetAllPaginatedAsync(string? searchQuery, int page, int pageSize);
        Task<List<EventoVM>> GetLatestAsync(int count);
        Task<EventoVM?> GetByIdAsync(int id);
        Task<ServiceResult<EventoVM>> CreateAsync(EventoVM eventoVM);
        Task<ServiceResult<EventoVM>> UpdateAsync(EventoVM eventoVM);
        Task<ServiceResult<bool>> CancelAsync(int id);
    }
}