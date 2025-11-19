using si_td_gestion_eventos.Infrastructure;
using si_td_gestion_eventos.Models.Enums;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Common;

namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IFianzaService
    {
        Task<ServiceResult<FianzaVM>> CreateAsync(FianzaVM fianzaVM);
        Task<PaginatedList<FianzaVM>> GetPaginatedAsync(string? q, EstadoFianza? estado, int page, int pageSize);
    }
}