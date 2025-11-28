using System.Collections.Generic;
using System.Threading.Tasks;
using si_td_gestion_eventos.Models.ViewModels;
using si_td_gestion_eventos.Services.Common;

namespace si_td_gestion_eventos.Services.Contracts
{
    public interface IServicioEsencialService
    {
        Task<List<ServicioEsencialVM>> GetByEventoIdAsync(int eventoId);
        Task<ServiceResult<bool>> InicializarServiciosAsync(int eventoId);
        Task<ServiceResult<ServicioEsencialVM>> SubirComprobanteAsync(int id, ServicioEsencialVM modelo);
        Task<ServiceResult<bool>> VerificarServicioAsync(int id);
        Task<ServiceResult<bool>> EliminarComprobanteAsync(int id);
    }
}