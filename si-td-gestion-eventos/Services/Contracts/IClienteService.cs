using Microsoft.AspNetCore.Mvc.Rendering;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Common;

namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IClienteService
    {
        Task<IEnumerable<ClienteVM>> GetAllAsync();
        Task<ClienteVM?> GetByIdAsync(int id);
        Task<ServiceResult<ClienteVM>> CreateAsync(ClienteVM clienteVM);
        Task<ServiceResult<ClienteVM>> UpdateAsync(ClienteVM clienteVM);
        Task<ServiceResult<bool>> DeactivateAsync(int id);
        Task<ServiceResult<bool>> ActivateAsync(int id);
        Task<IEnumerable<SelectListItem>> GetClientesActivosParaDropdownAsync();
    }
}