using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace si_td_gestion_eventos.Entities
{

    public class Cliente
    {
        public int ClienteId { get; set; }

        [Required]
        public TipoCliente Tipo { get; set; }
        [Required]
        [StringLength(100)]
        public string Nombre { get; set; }
        [StringLength(60)]
        public string? Apellido { get; set; }
        [StringLength(30)]
        [Display(Name = "Cédula de Identidad")]
        public string? CedulaIdentidad { get; set; }

        [Required]
        [StringLength(120)]
        public string Domicilio { get; set; }

        [Required]
        [StringLength(30)]
        [Phone]
        public string Telefono { get; set; }

        public bool Activo { get; set; } = true;

        [MaxLength(12)]
        public string? RUT { get; set; }

        public ICollection<Evento> Eventos { get; set; } = new List<Evento>();
    }
}