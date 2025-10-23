using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Entities;
using si_td_gestion_eventos.Models.Enums; // Asegúrate de importar tus Enums

namespace si_td_gestion_eventos.Context
{
    public class AppDbContext : DbContext
    {
        // --- AQUÍ ESTÁ EL CONSTRUCTOR QUE FALTA ---
        // Este constructor recibe las 'options' (como la cadena de conexión)
        // desde Program.cs y las pasa a la clase base DbContext.
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // No necesita nada adentro
        }
        // --- FIN DEL CONSTRUCTOR ---

        // Tus DbSets (Cliente, Evento, etc.)
        public virtual DbSet<Cliente> Cliente { get; set; }
        public virtual DbSet<Evento> Evento { get; set; }
        public virtual DbSet<Pago> Pago { get; set; }
        public virtual DbSet<Fianza> Fianza { get; set; }
        public virtual DbSet<ComprobanteExterno> ComprobanteExterno { get; set; }
        public virtual DbSet<Reporte> Reporte { get; set; }

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
                    new Cliente { ClienteId = 1, Nombre = "test", Apellido = "test", CedulaIdentidad = "131331313", Domicilio = "calle", Telefono = "47832", Tipo = TipoCliente.PersonaFisica, Activo = true, RUT = null }
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
                e.OwnsOne(ev => ev.ServiciosEsenciales, se =>
                {
                    se.OwnsOne(s => s.CertificadoAGADU);
                });
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
                f.HasOne(fi => fi.Evento).WithMany(ev => ev.Fianzas).HasForeignKey(fi => fi.EventoId).OnDelete(DeleteBehavior.Cascade);
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
        }
    }
}