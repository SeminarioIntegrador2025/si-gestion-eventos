using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums;

namespace si_td_gestion_eventos.Context
{
    public class AppDbContext : DbContext
    {

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public virtual DbSet<Cliente> Cliente { get; set; }
        public virtual DbSet<Evento> Evento { get; set; }
        public virtual DbSet<Pago> Pago { get; set; }
        public virtual DbSet<Fianza> Fianza { get; set; }
        public virtual DbSet<ComprobanteExterno> ComprobanteExterno { get; set; }
        public virtual DbSet<Reporte> Reporte { get; set; }
        public DbSet<ServicioEsencial> ServiciosEsenciales { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de Cliente (índices filtrados, seed data)
            modelBuilder.Entity<Cliente>(c =>
            {
                c.HasKey("ClienteId");
                c.Property("ClienteId").ValueGeneratedOnAdd();
                c.HasIndex(e => e.CedulaIdentidad).IsUnique().HasFilter("[CedulaIdentidad] IS NOT NULL");
                c.HasIndex(e => e.RUT).IsUnique().HasFilter("[RUT] IS NOT NULL");
                c.HasData(
                    new Cliente { ClienteId = 1, Nombre = "Juan", Apellido = "Perez", CedulaIdentidad = "12345678", Domicilio = "Calle Falsa", Telefono = "47832777", Tipo = TipoCliente.PersonaFisica, Activo = true, RUT = null }
                );
            });

            // Configuración de Evento (relaciones, owned types)
            modelBuilder.Entity<Evento>(e =>
            {
                e.HasKey("EventoId");
                e.Property("EventoId").ValueGeneratedOnAdd();

                // Relación Cliente -> Evento
                e.HasOne(ev => ev.Cliente)
                    .WithMany(c => c.Eventos)
                    .HasForeignKey(ev => ev.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Owned Types
                e.OwnsOne(ev => ev.ResponsableSalon);
            });

            // Configuración de Pago (relación con Evento)
            modelBuilder.Entity<Pago>(p =>
            {
                p.HasKey("PagoId");
                p.Property("PagoId").ValueGeneratedOnAdd();
                p.HasOne(pa => pa.Evento).WithMany(ev => ev.Pagos).HasForeignKey(pa => pa.EventoId).OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración de Fianza (relación con Evento)
            modelBuilder.Entity<Fianza>(f =>
            {
                f.HasKey("FianzaId");
                f.Property("FianzaId").ValueGeneratedOnAdd();
                f.HasOne(fi => fi.Evento)
                 .WithOne(ev => ev.Fianza)
                 .HasForeignKey<Fianza>(fi => fi.EventoId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración de Reporte (relación con Evento)
            modelBuilder.Entity<Reporte>(r =>
            {
                r.HasKey("ReporteId");
                r.Property("ReporteId").ValueGeneratedOnAdd();
                r.HasOne(re => re.Evento).WithMany(ev => ev.Reportes).HasForeignKey(re => re.EventoId).OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración de ComprobanteExterno (relación 1-a-1 con Pago)
            modelBuilder.Entity<ComprobanteExterno>(ce =>
            {
                ce.HasKey("ComprobanteExternoId");
                ce.Property("ComprobanteExternoId").ValueGeneratedOnAdd();
                ce.HasOne(c => c.Pago).WithOne(p => p.ComprobanteExterno).HasForeignKey<ComprobanteExterno>(c => c.PagoId).OnDelete(DeleteBehavior.Restrict);
            });


            //Configuración de Servicios Esenciales (TPH - Herencia)

            modelBuilder.Entity<ServicioEsencial>(se =>
            {
                se.HasKey(s => s.Id);
                se.Property(s => s.Id).ValueGeneratedOnAdd();

                // Estrategia TPH: Una sola tabla con columna discriminadora
                se.HasDiscriminator<string>("TipoServicio")
                  .HasValue<CertificadoAGADU>("AGADU");

                // Relación 1 Evento -> N Servicios
                se.HasOne(s => s.Evento)
                  .WithMany(e => e.ServiciosEsenciales)
                  .HasForeignKey(s => s.EventoId)
                  .OnDelete(DeleteBehavior.Cascade);
            });

        }
    }
}