
namespace si_td_gestion_eventos.Models.ViewModels
{
    public class ClienteVM
    {

        public int ClienteId { get; set; }
        public required string Nombre { get; set; }
        public required string Apellido { get; set; }
        public required string CedulaIdentidad { get; set; }

        public required string Domicilio { get; set; }

        public required string Telefono { get; set; }
        public required Boolean Activo { get; set; }

    }
}
