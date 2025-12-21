using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Common;

namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IPagoService
    {
        Task<List<PagoVM>> GetPagosByEventoIdAsync(int eventoId);
        Task<PagoVM?> GetByIdAsync(int id);
        Task<ServiceResult<PagoVM>> CreateAsync(PagoVM pagoVM);
        Task<List<PagoVM>> GetAllAsync();
        Task<byte[]?> GenerarReciboPdfAsync(int pagoId);
        Task<ServiceResult<bool>> AnularPagoAsync(int pagoId);
        Task<ServiceResult<bool>> UpdateAsync(PagoVM model);
        // Task<ServiceResult<PagoVM>> UpdateAsync(PagoVM pagoVM);
        // Task<ServiceResult> DeleteAsync(int pagoId);


    }
}