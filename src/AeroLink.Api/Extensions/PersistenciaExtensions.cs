using AeroLink.Api.Data;
using AeroLink.Api.Repositories;
using AeroLink.Api.Repositories.Ef;
using Microsoft.EntityFrameworkCore;

namespace AeroLink.Api.Extensions;

public static class PersistenciaExtensions
{
    //EF Core + SQLite y los repositorios (Unidad 4).
    public static IServiceCollection AddAeroLinkPersistencia(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AeroLinkDbContext>(o =>
            o.UseSqlite(config.GetConnectionString("AeroLinkDb") ?? "Data Source=aerolink.db"));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AeroLinkDbContext>());
        services.AddScoped<IVueloRepository, EfVueloRepository>();
        services.AddScoped<IPasajeroRepository, EfPasajeroRepository>();
        services.AddScoped<IReservaRepository, EfReservaRepository>();
        services.AddScoped<ITicketRepository, EfTicketRepository>();
        return services;
    }

    // Aplica las migraciones al iniciar. Si todavía no se generó ninguna migración
    // (dotnet ef migrations add Inicial) crea el esquema directamente desde el modelo.
    public static void InicializarBaseDeDatos(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AeroLinkDbContext>();
        if (db.Database.GetMigrations().Any()) db.Database.Migrate();
        else db.Database.EnsureCreated();
    }
}
