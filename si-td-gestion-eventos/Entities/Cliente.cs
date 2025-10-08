using System.ComponentModel.DataAnnotations;

namespace si_td_gestion_eventos.Entities
{
    public class Cliente
    {
        public int ClienteId { get; set; }
        public required string Nombre { get; set; }
        public required string Apellido { get; set; }
        public required string CedulaIdentidad { get; set; }
        public required string Domicilio { get; set; }
        public required string Telefono { get; set; }
        public bool Activo { get; set; } = true;

        // relaciones con otros modelos
        public ICollection<Evento> Eventos { get; set; } = new List<Evento>();
    }
}
