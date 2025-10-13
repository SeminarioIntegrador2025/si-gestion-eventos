using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Entities;

namespace si_td_gestion_eventos.Context
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) 
        {
           
        }

        public virtual DbSet<Cliente> Cliente { get; set; }
        public virtual DbSet<Evento> Evento { get; set; }
        public virtual DbSet<Fianza> Fianza { get; set; }
        public virtual DbSet<Pago> Pago { get; set; }
        public virtual DbSet<ComprobanteExterno> ComprobanteExterno { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Cliente>(c =>
            {
                c.HasKey("ClienteId");
                c.Property("ClienteId").ValueGeneratedOnAdd();
                c.HasData(
                    new Cliente { ClienteId = 1, Nombre = "test" , Apellido = "test", CedulaIdentidad = "131331313", Domicilio = "calle", Telefono = "47832"});
            });

            // Relación Cliente - Evento (uno a muchos)
            modelBuilder.Entity<Evento>(e =>
            {
                e.HasKey("EventoId");
                e.Property("EventoId").ValueGeneratedOnAdd();
                e.HasOne(ev => ev.Cliente)
                    .WithMany(c => c.Eventos)
                    .HasForeignKey(ev => ev.ClienteId)
                    .OnDelete(DeleteBehavior.Cascade);

            });

            // Relación Pago - ComprobanteExterno (uno a uno)
            modelBuilder.Entity<ComprobanteExterno>()
                .HasOne(ce => ce.Pago)
                .WithOne(p => p.ComprobanteExterno)
                .HasForeignKey<ComprobanteExterno>(ce => ce.PagoId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Evento>()
                .HasMany(p => p.Pagos)
                .WithOne(e => e.Evento)
                .HasForeignKey(p => p.PagoId)
                .OnDelete(DeleteBehavior.Cascade);
        }


    }
}

