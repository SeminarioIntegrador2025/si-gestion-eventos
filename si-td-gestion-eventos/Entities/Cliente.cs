using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;


namespace si_td_gestion_eventos.Entities
{
    [Index(nameof(CedulaIdentidad), IsUnique = true)]
    public class Cliente
    {
        public int ClienteId { get; set; }
        [Required, StringLength(60)]
        public required string Nombre { get; set; }
        [Required, StringLength(60)]
        public required string Apellido { get; set; }
        [Required, StringLength(30)]
        [Display(Name = "Cédula de Identidad")]
        public required string CedulaIdentidad { get; set; }
        [Required, StringLength(11)]
        public required string Domicilio { get; set; }
        [Required, StringLength(30)]
        [Phone]
        public required string Telefono { get; set; }
        public bool Activo { get; set; } = true;

        // relaciones con otros modelos
        public ICollection<Evento> Eventos { get; set; } = new List<Evento>();
    }
}
