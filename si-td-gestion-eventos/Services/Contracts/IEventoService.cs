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

        // --- CORRECCIONES AQUÍ ---
        // Se especifica que devuelve un ServiceResult que contiene un EventoVM
        Task<ServiceResult<EventoVM>> CreateAsync(EventoVM eventoVM);

        // Se especifica que devuelve un ServiceResult que contiene un EventoVM
        Task<ServiceResult<EventoVM>> UpdateAsync(EventoVM eventoVM);

        // Se especifica que devuelve un ServiceResult que contiene un booleano (true/false)
        Task<ServiceResult<bool>> CancelAsync(int id);
    }
}