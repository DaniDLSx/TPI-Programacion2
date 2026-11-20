using AeroLink.Api.Models;

namespace AeroLink.Api.Repositories;

//Confirma en la base los cambios hechos sobre las entidades (SaveChanges).
public interface IUnitOfWork
{
    Task GuardarCambiosAsync();
}

public interface IVueloRepository
{
    Task<List<Vuelo>> ListarAsync(EstadoVuelo? estado, string? origen, string? destino);
    Task<Vuelo?> ObtenerAsync(int id);
    //Vuelo con sus reservas, y de cada reserva su pasajero y ticket.
    Task<Vuelo?> ObtenerConReservasAsync(int id);
    Task<Vuelo?> ObtenerPorSlotIdAsync(string slotId);
    Task<Vuelo?> ObtenerPorCodigoAsync(string codigoVuelo);
    Task<bool> ExisteCodigoAsync(string codigoVuelo);
    void Agregar(Vuelo vuelo);
}

public interface IPasajeroRepository
{
    Task<List<Pasajero>> ListarAsync();
    Task<Pasajero?> ObtenerAsync(int id);
    Task<bool> ExisteDocumentoAsync(string documento);
    void Agregar(Pasajero pasajero);
}

public interface IReservaRepository
{
    //Reservas con pasajero, vuelo y ticket cargados.
    Task<List<Reserva>> ListarAsync(int? vueloId, int? pasajeroId, EstadoReserva? estado);
    Task<Reserva?> ObtenerAsync(int id);
    //¿Hay una reserva no anulada con ese asiento en ese vuelo?
    Task<bool> AsientoOcupadoAsync(int vueloId, string asiento);
    //¿El pasajero ya tiene una reserva no anulada en ese vuelo?
    Task<bool> ExisteReservaActivaAsync(int vueloId, int pasajeroId);
    void Agregar(Reserva reserva);
}

public interface ITicketRepository
{
    Task<List<Ticket>> ListarAsync(int? reservaId);
    Task<Ticket?> ObtenerPorCodigoAsync(string codigo);
    Task<bool> ExisteCodigoAsync(string codigo);
    void Agregar(Ticket ticket);
}
