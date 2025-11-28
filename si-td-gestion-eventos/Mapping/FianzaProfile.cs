using AutoMapper;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;

namespace si_td_gestion_eventos.Mapping
{
    public class FianzaProfile : Profile
    {
        public FianzaProfile()
        {
            // Mapeo: Entidad Fianza -> FianzaVM
            CreateMap<Fianza, FianzaVM>()
                // Mapeos para la LISTA (Index)
                .ForMember(dest => dest.ClienteNombre, opt => opt.MapFrom(src =>
                    $"{src.Evento.Cliente.Nombre} {src.Evento.Cliente.Apellido}"))

                .ForMember(dest => dest.EventoFecha, opt => opt.MapFrom(src => src.Evento.Inicio))

                // Esta te faltaba:
                .ForMember(dest => dest.EventoDescripcion, opt => opt.MapFrom(src => src.Evento.Observaciones));

            // NOTA: No necesitas mapear Monto, FechaRegistro, etc.
            // si se llaman exactamente igual en ambas clases. AutoMapper lo hace solo.

            // Mapeo en reversa (del VM a la Entidad)
            CreateMap<FianzaVM, Fianza>()
                // Ignoramos 'Evento' porque SÍ existe en Fianza, 
                // pero no queremos que AutoMapper intente crearlo desde el VM.
                .ForMember(dest => dest.Evento, opt => opt.Ignore());

            // No necesitas hacer NADA para ClienteNombre, EventoFecha, o EventoDescripcion.
            // Como no existen en la entidad Fianza, AutoMapper los ignora automáticamente
            // y ya no tendrás el error de compilación.
        }
    }
}