using Microsoft.EntityFrameworkCore;

namespace si_td_gestion_eventos.Entities
{
    [Owned] // Le dice a EF que esta clase no es una tabla
    public class ResponsableSalon
    {
        public string Nombre { get; set; }
        public string CI { get; set; }
        public string Telefono { get; set; }
    }
}