using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Entities;

namespace si_td_gestion_eventos.Infrastructure;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Fianza> Fianzas => Set<Fianza>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>()
            .HasIndex(c => c.CedulaIdentidad)
            .IsUnique();

        modelBuilder.Entity<Evento>()
            .HasOne(e => e.Fianza)
            .WithOne(f => f.Evento)
            .HasForeignKey<Fianza>(f => f.EventoId);

        modelBuilder.Entity<Evento>()
            .HasIndex(e => new { e.Inicio, e.Fin, e.Estado });

        // Configuración de precisión para decimales en Evento
        modelBuilder.Entity<Evento>()
            .Property(e => e.CostoAlquiler)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Evento>()
            .Property(e => e.MontoReserva)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Evento>()
            .Property(e => e.MontoAireAcondicionado)
            .HasPrecision(18, 2);

        // Configuración de precisión para decimales en Fianza
        modelBuilder.Entity<Fianza>()
            .Property(f => f.Monto)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Fianza>()
            .Property(f => f.MontoDevuelto)
            .HasPrecision(18, 2);

        // Configuración de precisión para decimales en Pago
        modelBuilder.Entity<Pago>()
            .Property(p => p.Monto)
            .HasPrecision(18, 2);

        base.OnModelCreating(modelBuilder);
    }
}
