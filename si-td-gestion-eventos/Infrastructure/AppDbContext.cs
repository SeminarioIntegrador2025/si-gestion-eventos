using Microsoft.EntityFrameworkCore;
using si_td_gestion_eventos.Models;
using si_td_gestion_eventos.Domain;

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

        // índice útil para disponibilidad
        modelBuilder.Entity<Evento>()
            .HasIndex(e => new { e.Inicio, e.Fin, e.Estado });

        base.OnModelCreating(modelBuilder);
    }
}
