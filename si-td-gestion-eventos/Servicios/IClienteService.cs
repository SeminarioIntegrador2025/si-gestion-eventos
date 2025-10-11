using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace si_td_gestion_eventos.Services
{
    public interface IClienteService
    {
        IEnumerable<SelectListItem> GetClientesActivosParaDropdown();
    }
}