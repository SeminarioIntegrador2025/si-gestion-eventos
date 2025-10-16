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
                          opt => opt.MapFrom(src => src.Cliente != null 
                              ? $"{src.Cliente.Nombre} {src.Cliente.Apellido}" 
                              : string.Empty))
                .ForMember(dest => dest.TotalPagado, opt => opt.MapFrom(src => 
                    src.Pagos != null ? src.Pagos.Sum(p => p.Monto) : 0))
                .ForMember(dest => dest.SaldoRestante, opt => opt.MapFrom(src => 
                    (src.CostoAlquiler + (src.MontoAireAcondicionado ?? 0)) - 
                    (src.Pagos != null ? src.Pagos.Sum(p => p.Monto) : 0)));

            // Mapeo de ViewModel a Entidad (para Create/Update)
            CreateMap<EventoVM, Evento>()
                .ForMember(dest => dest.Cliente, opt => opt.Ignore()) // Ignorar el mapeo inverso
                .ForMember(dest => dest.Pagos, opt => opt.Ignore())
                .ForMember(dest => dest.Fianza, opt => opt.Ignore());
        }
    }
}