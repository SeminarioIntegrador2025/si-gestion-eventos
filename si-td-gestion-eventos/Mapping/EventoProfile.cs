using AutoMapper;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using System.Linq; // Necesario para .FirstOrDefault() y .Sum()

namespace si_td_gestion_eventos.Mapping
{
    public class EventoProfile : Profile
    {
        public EventoProfile()
        {
            // --- Mapeo de Evento (Entidad) HACIA EventoVM (ViewModel) ---
            CreateMap<Evento, EventoVM>()
                // Mapeo de Cliente (como ya lo tenías)
                .ForMember(dest => dest.ClienteNombreCompleto,
                           opt => opt.MapFrom(src => src.Cliente != null
                               ? $"{src.Cliente.Nombre} {src.Cliente.Apellido}"
                               : string.Empty))

                // --- ARREGLO 1: Mapeo de Responsable ---
                // Le decimos que saque los datos del objeto anidado 'ResponsableSalon'
                .ForMember(dest => dest.ResponsableNombre, opt => opt.MapFrom(src => src.ResponsableSalon.Nombre))
                .ForMember(dest => dest.ResponsableTelefono, opt => opt.MapFrom(src => src.ResponsableSalon.Telefono))
                .ForMember(dest => dest.ResponsableCedula, opt => opt.MapFrom(src => src.ResponsableSalon.CI)) // Asumiendo que CI en ResponsableSalon mapea a Cedula en VM

                // Mapeo de Pagos (como ya lo tenías, asumiendo que Pago.Monto es float)
                .ForMember(dest => dest.TotalPagado, opt => opt.MapFrom(src =>
                    src.Pagos != null ? src.Pagos.Sum(p => p.Monto) : 0f)) // Usamos 0f para float

                // Mapeo de Saldo (como ya lo tenías, asumiendo que CostoAlquiler etc. son float)
                .ForMember(dest => dest.SaldoRestante, opt => opt.MapFrom(src =>
                    (src.CostoAlquiler + (src.MontoAireAcondicionado ?? 0f)) -
                    (src.Pagos != null ? src.Pagos.Sum(p => p.Monto) : 0f))) // Usamos 0f para float

                .ForMember(dest => dest.TipoCli,
                       opt => opt.MapFrom(src => src.Cliente.Tipo))

                // --- ARREGLO 2: Mapeo de Fianza/Fianzas ---
                // Asumimos que EventoVM tiene una sola propiedad 'Fianza' o 'FianzaVM'.
                // Mapeamos la primera fianza encontrada en la colección. Si no hay, será null.
                .ForMember(dest => dest.DetalleFianza, opt => opt.MapFrom(src => src.Fianzas.FirstOrDefault()));
            // Si tu EventoVM tiene una List<FianzaVM>, comenta la línea de arriba y descomenta esta:
            // .ForMember(dest => dest.FianzasVM, opt => opt.MapFrom(src => src.Fianzas)) // Necesitarías un CreateMap<Fianza, FianzaVM>();


            // --- Mapeo de EventoVM (ViewModel) HACIA Evento (Entidad) ---
            // En Mapping/EventoProfile.cs

            // --- Mapeo de EventoVM (ViewModel) HACIA Evento (Entidad) ---
            CreateMap<EventoVM, Evento>()
                // Ignoramos las propiedades de navegación
                .ForMember(dest => dest.Cliente, opt => opt.Ignore())
                .ForMember(dest => dest.Pagos, opt => opt.Ignore())
                .ForMember(dest => dest.Fianzas, opt => opt.Ignore())
                .ForMember(dest => dest.Reportes, opt => opt.Ignore())
                // ForPath mapea las propiedades INTERNAS de ResponsableSalon
                .ForPath(dest => dest.ResponsableSalon.Nombre, opt => opt.MapFrom(src => src.ResponsableNombre))
                .ForPath(dest => dest.ResponsableSalon.Telefono, opt => opt.MapFrom(src => src.ResponsableTelefono))
                .ForPath(dest => dest.ResponsableSalon.CI, opt => opt.MapFrom(src => src.ResponsableCedula))

                // --- ¡ESTA ES LA MAGIA! ---
                // Después de que AutoMapper haga el mapeo básico, ejecutamos esto:
                .AfterMap((src, dest) => {
                    // Aseguramos que los objetos anidados requeridos existan
                    if (dest.ResponsableSalon == null)
                    {
                        // Si ForPath no lo creó (a veces pasa), lo creamos aquí
                        // y AutoMapper debería haber mapeado las propiedades internas igual
                        dest.ResponsableSalon = new ResponsableSalon
                        {
                            Nombre = src.ResponsableNombre,
                            Telefono = src.ResponsableTelefono,
                            CI = src.ResponsableCedula
                        };
                    }
                    if (dest.ServiciosEsenciales == null)
                    {
                        // Creamos ServiciosEsenciales y su CertificadoAGADU interno
                        dest.ServiciosEsenciales = new ServiciosEsenciales
                        {
                            CertificadoAGADU = new CertificadoAGADU()
                            // Aquí podrías mapear datos de AGADU si vinieran del VM
                            // ej: RutaArchivo = src.RutaCertificadoAgadu (si existiera en EventoVM)
                        };
                    }
                });

            // --- OTROS MAPEOS (si los necesitas) ---
            // Si mapeaste la lista de fianzas, necesitarás este mapeo:
            CreateMap<Fianza, FianzaVM>();
             CreateMap<FianzaVM, Fianza>();
        }
    }
}