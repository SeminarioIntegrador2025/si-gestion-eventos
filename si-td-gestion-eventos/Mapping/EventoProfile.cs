using AutoMapper;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.ViewModels;
using System.Linq;
using System.Reflection; // Necesario para obtener propiedades dinámicamente

namespace si_td_gestion_eventos.Mapping
{
    public class EventoProfile : Profile
    {
        public EventoProfile()
        {
            // =========================================================================
            // 1. MAPEO DE SERVICIO ESENCIAL (Entidad Abstracta -> VM Concreto)
            // =========================================================================
            CreateMap<ServicioEsencial, ServicioEsencialVM>()
                // A. Tipo: Usamos el nombre de la clase (Ej: Agadu, Sadaic)
                .ForMember(dest => dest.TipoServicio, opt => opt.MapFrom(src => src.GetType().Name))

                // B. RutaArchivo: La buscamos dinámicamente porque la clase base no la tiene
                .ForMember(dest => dest.RutaArchivo, opt => opt.MapFrom(src => GetValorPropiedad(src, "Archivo")))

                // C. Verificado: Si hay ruta, está verificado
                .ForMember(dest => dest.Verificado, opt => opt.MapFrom(src =>
                    !string.IsNullOrEmpty(GetValorPropiedad(src, "Archivo"))))

                // D. FechaAdjunto: Intentamos buscarla también dinámicamente
                .ForMember(dest => dest.FechaAdjunto, opt => opt.Ignore()); // O usa GetValorPropiedad si tienes esa fecha en la BD


            // =========================================================================
            // 2. MAPEO DE EVENTO (Entidad -> VM)
            // =========================================================================
            CreateMap<Evento, EventoVM>()
                // --- CONEXIÓN CLAVE: Mapeamos la lista de servicios ---
                // AutoMapper usará la configuración del punto 1 para cada elemento de la lista
                .ForMember(dest => dest.ServiciosEsenciales, opt => opt.MapFrom(src => src.ServiciosEsenciales))

                // ... Tus otros mapeos existentes ...
                .ForMember(dest => dest.ClienteNombreCompleto,
                           opt => opt.MapFrom(src => src.Cliente != null
                               ? $"{src.Cliente.Nombre} {src.Cliente.Apellido}" : string.Empty))

                .ForMember(dest => dest.ResponsableNombre, opt => opt.MapFrom(src => src.ResponsableSalon.Nombre))
                .ForMember(dest => dest.ResponsableTelefono, opt => opt.MapFrom(src => src.ResponsableSalon.Telefono))
                .ForMember(dest => dest.ResponsableCedula, opt => opt.MapFrom(src => src.ResponsableSalon.CI))

                .ForMember(dest => dest.TotalPagado, opt => opt.MapFrom(src =>
                    src.Pagos != null ? src.Pagos.Sum(p => p.Monto) : 0f))

                .ForMember(dest => dest.SaldoRestante, opt => opt.MapFrom(src =>
                    (src.CostoAlquiler + (src.MontoAireAcondicionado ?? 0f)) -
                    (src.Pagos != null ? src.Pagos.Sum(p => p.Monto) : 0f)))

                .ForMember(dest => dest.TipoCli, opt => opt.MapFrom(src => src.Cliente.Tipo))
                .ForMember(dest => dest.FianzaId, opt => opt.MapFrom(src => src.FianzaId))
                .ForMember(dest => dest.DetalleFianza, opt => opt.MapFrom(src => src.Fianza));


            // =========================================================================
            // 3. MAPEO INVERSO (VM -> Entidad)
            // =========================================================================
            CreateMap<EventoVM, Evento>()
                .ForMember(dest => dest.Cliente, opt => opt.Ignore())
                .ForMember(dest => dest.Pagos, opt => opt.Ignore())
                .ForMember(dest => dest.Fianza, opt => opt.Ignore())
                .ForMember(dest => dest.Reportes, opt => opt.Ignore())
                // Ignoramos la lista al guardar para evitar conflictos de EF Core
                .ForMember(dest => dest.ServiciosEsenciales, opt => opt.Ignore())

                .ForPath(dest => dest.ResponsableSalon.Nombre, opt => opt.MapFrom(src => src.ResponsableNombre))
                .ForPath(dest => dest.ResponsableSalon.Telefono, opt => opt.MapFrom(src => src.ResponsableTelefono))
                .ForPath(dest => dest.ResponsableSalon.CI, opt => opt.MapFrom(src => src.ResponsableCedula));
        }

        // -----------------------------------------------------------------------------
        // MÉTODO HELPER: Extrae valores de propiedades que no existen en la clase base
        // -----------------------------------------------------------------------------
        private string GetValorPropiedad(object src, string nombrePropiedad)
        {
            if (src == null) return null;

            // Busca la propiedad en la clase derivada (ej: Agadu.Archivo)
            var prop = src.GetType().GetProperty(nombrePropiedad);

            if (prop != null)
            {
                var val = prop.GetValue(src);
                return val?.ToString();
            }
            return null;
        }
    }
}