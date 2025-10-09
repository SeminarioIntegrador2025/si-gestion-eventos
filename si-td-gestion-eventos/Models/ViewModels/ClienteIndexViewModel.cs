using si_td_gestion_eventos.Entities;

namespace si_td_gestion_eventos.Models.ViewModels
{
    public class ClienteIndexViewModel
    {
        public List<Cliente> Clientes { get; set; } = new();
        public List<Cliente> UltimosClientes { get; set; } = new();
    }
}
