using AeroLink.Api.Data;
using AeroLink.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AeroLink.Api.Repositories.Ef;

public class EfVueloRepository : IVueloRepository
{
    private readonly AeroLinkDbContext _db;
    public EfVueloRepository(AeroLinkDbContext db) => _db = db;

    public Task<List<Vuelo>> ListarAsync(EstadoVuelo? estado, string? origen, string? destino)
    {
        var q = _db.Vuelos.AsNoTracking().AsQueryable();
        if (estado.HasValue) q = q.Where(v => v.Estado == estado.Value);
        if (!string.IsNullOrEmpty(origen)) q = q.Where(v => v.Origen == origen);
        if (!string.IsNullOrEmpty(destino)) q = q.Where(v => v.Destino == destino);
        return q.OrderBy(v => v.HorarioSalida).ThenBy(v => v.Id).ToListAsync();
    }

    public Task<Vuelo?> ObtenerAsync(int id) => _db.Vuelos.FirstOrDefaultAsync(v => v.Id == id);

    public Task<Vuelo?> ObtenerConReservasAsync(int id) => _db.Vuelos
        .Include(v => v.Reservas).ThenInclude(r => r.Ticket)
        .Include(v => v.Reservas).ThenInclude(r => r.Pasajero)
        .FirstOrDefaultAsync(v => v.Id == id);

    public Task<Vuelo?> ObtenerPorSlotIdAsync(string slotId) =>
        _db.Vuelos.FirstOrDefaultAsync(v => v.SlotId == slotId);

    public Task<Vuelo?> ObtenerPorCodigoAsync(string codigoVuelo) =>
        _db.Vuelos.FirstOrDefaultAsync(v => v.CodigoVuelo == codigoVuelo);

    public Task<bool> ExisteCodigoAsync(string codigoVuelo) =>
        _db.Vuelos.AnyAsync(v => v.CodigoVuelo == codigoVuelo);

    public Task<List<Vuelo>> ListarConReservasAsync(EstadoVuelo? estado)
    {
        var q = _db.Vuelos.AsNoTracking()
            .Include(v => v.Reservas).ThenInclude(r => r.Ticket)
            .Include(v => v.Reservas).ThenInclude(r => r.Pasajero)
            .AsQueryable();
        if (estado.HasValue) q = q.Where(v => v.Estado == estado.Value);
        return q.OrderBy(v => v.HorarioSalida).ThenBy(v => v.Id).ToListAsync();
    }

    public void Agregar(Vuelo vuelo) => _db.Vuelos.Add(vuelo);
}

public class EfPasajeroRepository : IPasajeroRepository
{
    private readonly AeroLinkDbContext _db;
    public EfPasajeroRepository(AeroLinkDbContext db) => _db = db;

    public Task<List<Pasajero>> ListarAsync() =>
        _db.Pasajeros.AsNoTracking().OrderBy(p => p.Apellido).ThenBy(p => p.Nombre).ToListAsync();

    public Task<Pasajero?> ObtenerAsync(int id) => _db.Pasajeros.FirstOrDefaultAsync(p => p.Id == id);

    public Task<bool> ExisteDocumentoAsync(string documento) =>
        _db.Pasajeros.AnyAsync(p => p.Documento == documento);

    public void Agregar(Pasajero pasajero) => _db.Pasajeros.Add(pasajero);
}

public class EfReservaRepository : IReservaRepository
{
    private readonly AeroLinkDbContext _db;
    public EfReservaRepository(AeroLinkDbContext db) => _db = db;

    private IQueryable<Reserva> ConDetalle() => _db.Reservas
        .Include(r => r.Pasajero)
        .Include(r => r.Vuelo)
        .Include(r => r.Ticket);

    public Task<List<Reserva>> ListarAsync(int? vueloId, int? pasajeroId, EstadoReserva? estado)
    {
        var q = ConDetalle().AsNoTracking();
        if (vueloId.HasValue) q = q.Where(r => r.VueloId == vueloId.Value);
        if (pasajeroId.HasValue) q = q.Where(r => r.PasajeroId == pasajeroId.Value);
        if (estado.HasValue) q = q.Where(r => r.Estado == estado.Value);
        return q.OrderBy(r => r.Id).ToListAsync();
    }

    public Task<Reserva?> ObtenerAsync(int id) => ConDetalle().FirstOrDefaultAsync(r => r.Id == id);

    public Task<bool> AsientoOcupadoAsync(int vueloId, string asiento) =>
        _db.Reservas.AnyAsync(r => r.VueloId == vueloId && r.Asiento == asiento && r.Estado != EstadoReserva.Anulada);

    public Task<bool> ExisteReservaActivaAsync(int vueloId, int pasajeroId) =>
        _db.Reservas.AnyAsync(r => r.VueloId == vueloId && r.PasajeroId == pasajeroId && r.Estado != EstadoReserva.Anulada);

    public void Agregar(Reserva reserva) => _db.Reservas.Add(reserva);
}

public class EfTicketRepository : ITicketRepository
{
    private readonly AeroLinkDbContext _db;
    public EfTicketRepository(AeroLinkDbContext db) => _db = db;

    private IQueryable<Ticket> ConDetalle() => _db.Tickets
        .Include(t => t.Reserva).ThenInclude(r => r!.Vuelo)
        .Include(t => t.Reserva).ThenInclude(r => r!.Pasajero);

    public Task<List<Ticket>> ListarAsync(int? reservaId)
    {
        var q = ConDetalle().AsNoTracking();
        if (reservaId.HasValue) q = q.Where(t => t.ReservaId == reservaId.Value);
        return q.OrderBy(t => t.Id).ToListAsync();
    }

    public Task<Ticket?> ObtenerPorCodigoAsync(string codigo) =>
        ConDetalle().FirstOrDefaultAsync(t => t.Codigo == codigo);

    public Task<bool> ExisteCodigoAsync(string codigo) => _db.Tickets.AnyAsync(t => t.Codigo == codigo);

    public void Agregar(Ticket ticket) => _db.Tickets.Add(ticket);
}
