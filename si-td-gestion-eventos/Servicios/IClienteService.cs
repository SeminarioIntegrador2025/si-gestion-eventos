using Microsoft.AspNetCore.Mvc.Rendering;

namespace si_td_gestion_eventos.Services
{
    public interface IClienteService
    {
        SelectList GetClientesActivosParaDropdown();
    }
}