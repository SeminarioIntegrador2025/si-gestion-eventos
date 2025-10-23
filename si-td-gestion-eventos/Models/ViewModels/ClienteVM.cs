using si_td_gestion_eventos.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace si_td_gestion_eventos.Models.ViewModels
{
    public class ClienteVM
    {
        public int ClienteId { get; set; }

        [Display(Name = "Tipo de Cliente")]
        [Required(ErrorMessage = "Debe seleccionar un tipo de cliente.")]
        public TipoCliente Tipo { get; set; }

        [Display(Name = "Nombre")] 
        [Required(ErrorMessage = "El nombre o razón social es obligatorio.")]
        public required string Nombre { get; set; }

        [Display(Name = "Apellido")]
        public string? Apellido { get; set; } 

        [Display(Name = "Cédula de Identidad")]
        public string? CedulaIdentidad { get; set; }

        [Display(Name = "RUT")]
        public string? RUT { get; set; }

        [Display(Name = "Teléfono")]
        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        public required string Telefono { get; set; }

        [Display(Name = "Domicilio")]
        [Required(ErrorMessage = "El domicilio es obligatorio.")]
        public required string Domicilio { get; set; }
        public required bool Activo { get; set; }

    }
}
