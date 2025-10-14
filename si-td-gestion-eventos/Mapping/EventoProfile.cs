using AutoMapper;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;

namespace si_td_gestion_eventos.Mapping
{
    public class EventoProfile : Profile
    {
        public EventoProfile()
        {
            // Mapeo de Entidad a ViewModel
            CreateMap<Evento, EventoVM>()
                .ForMember(dest => dest.ClienteNombreCompleto,
                           opt => opt.MapFrom(src => $"{src.Cliente.Apellido}, {src.Cliente.Nombre}"))
                .ForMember(dest => dest.TotalPagado,
                           opt => opt.MapFrom(src => src.Pagos.Sum(p => p.Monto)))
                .ForMember(dest => dest.SaldoRestante,
                           opt => opt.MapFrom(src => src.CostoAlquiler - src.Pagos.Sum(p => p.Monto)));

            // Mapeo de ViewModel a Entidad (para Create/Update)
            CreateMap<EventoVM, Evento>();
        }
    }
}