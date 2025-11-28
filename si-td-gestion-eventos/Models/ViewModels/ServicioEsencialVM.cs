using Microsoft.AspNetCore.Http; // Para IFormFile (subida de archivos)
using System;

namespace si_td_gestion_eventos.Models.ViewModels
{
    public class ServicioEsencialVM
    {
        public int Id { get; set; }
        public int EventoId { get; set; }

        //AGADU o otro tipo de servicio en un futuro
        public string TipoServicio { get; set; }

        public string? RutaArchivo { get; set; }
        public DateTime? FechaAdjunto { get; set; }
        public bool Verificado { get; set; }

        // Propiedad calculada para mostrar en la vista (Badge)
        public string EstadoDescripcion
        {
            get
            {
                // Si está verificado (que ocurre automáticamente al subir archivo)
                if (Verificado) return "Completado / Verificado";

                // Si no tiene archivo
                return "Pendiente (Falta comprobante)";
            }
        }

        // Para subir el archivo desde el formulario
        public IFormFile? ArchivoSubido { get; set; }
    }
}