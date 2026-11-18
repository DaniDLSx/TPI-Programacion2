using AeroLink.Api.Models;
using AeroLink.Api.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AeroLink.Api.Data;

//Sesión con la base de datos. También cumple el rol de IUnitOfWork.
public class AeroLinkDbContext : DbContext, IUnitOfWork
{
    public AeroLinkDbContext(DbContextOptions<AeroLinkDbContext> options) : base(options) { }

    public DbSet<Vuelo> Vuelos => Set<Vuelo>();
    public DbSet<Pasajero> Pasajeros => Set<Pasajero>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public Task GuardarCambiosAsync() => SaveChangesAsync();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite no guarda la "zona" de las fechas: siempre las leemos como UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vuelo>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.CodigoVuelo).IsRequired().HasMaxLength(10);
            e.Property(v => v.Origen).IsRequired().HasMaxLength(3);
            e.Property(v => v.Destino).IsRequired().HasMaxLength(3);
            e.Property(v => v.Aeronave).IsRequired().HasMaxLength(20);
            e.Property(v => v.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(v => v.EstadoSlot).HasConversion<string>().HasMaxLength(20);
            e.Property(v => v.SlotId).HasMaxLength(60);
            e.Property(v => v.Gate).HasMaxLength(10);
            e.HasIndex(v => v.CodigoVuelo).IsUnique();
            e.HasIndex(v => v.SlotId);
        });

        modelBuilder.Entity<Pasajero>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Nombre).IsRequired().HasMaxLength(60);
            e.Property(p => p.Apellido).IsRequired().HasMaxLength(60);
            e.Property(p => p.Documento).IsRequired().HasMaxLength(15);
            e.Property(p => p.Email).IsRequired().HasMaxLength(120);
            e.HasIndex(p => p.Documento).IsUnique();
        });

        modelBuilder.Entity<Reserva>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Asiento).IsRequired().HasMaxLength(3);
            e.Property(r => r.Estado).HasConversion<string>().HasMaxLength(20);
            e.HasOne(r => r.Vuelo).WithMany(v => v.Reservas)
                .HasForeignKey(r => r.VueloId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Pasajero).WithMany(p => p.Reservas)
                .HasForeignKey(r => r.PasajeroId).OnDelete(DeleteBehavior.Restrict);
            // Un asiento no puede estar ocupado dos veces en el mismo vuelo (las anuladas liberan el asiento).
            e.HasIndex(r => new { r.VueloId, r.Asiento }).IsUnique()
                .HasFilter("\"Estado\" <> 'Anulada'");
        });

        modelBuilder.Entity<Ticket>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Codigo).IsRequired().HasMaxLength(20);
            e.Property(t => t.Estado).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(t => t.Codigo).IsUnique();
            // Una reserva tiene como máximo un ticket.
            e.HasOne(t => t.Reserva).WithOne(r => r.Ticket)
                .HasForeignKey<Ticket>(t => t.ReservaId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.Kind == DateTimeKind.Utc ? v : (v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc)),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    { }
}
