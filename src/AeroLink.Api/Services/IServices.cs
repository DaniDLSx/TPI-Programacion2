using AeroLink.Api.Dtos;
using AeroLink.Api.Models;

namespace AeroLink.Api.Services;

public interface IVueloService
{
    Task<List<VueloDto>> ListarAsync(EstadoVuelo? estado, string? origen, string? destino);
    Task<VueloDto> ObtenerAsync(int id);
    Task<VueloDto> CrearAsync(VueloCrearDto dto);
    Task<VueloDto> ActualizarAsync(int id, VueloActualizarDto dto);
    //Pide un slot (pista + gate) al aeropuerto para el vuelo.
    Task<VueloDto> SolicitarSlotAsync(int id, SolicitarSlotDto dto);
    //Consulta al aeropuerto y, solo si el slot está otorgado, confirma el vuelo.
    Task<VueloDto> ConfirmarAsync(int id);
    //Cancela el vuelo, libera el slot y anula reservas y tickets.
    Task<CancelacionVueloDto> CancelarAsync(int id);
    //Procesa el callback del aeropuerto (slot otorgado o rechazado).
    Task<VueloDto> RecibirCallbackSlotAsync(CallbackSlotDto dto);
    //Desafío: vuelos + slot (local y aeropuerto) + reservas.
    Task<List<VueloConsolidadoDto>> ConsolidadoAsync(EstadoVuelo? estado);
}

public interface IPasajeroService
{
    Task<List<PasajeroDto>> ListarAsync();
    Task<PasajeroDto> ObtenerAsync(int id);
    Task<PasajeroDto> CrearAsync(PasajeroCrearDto dto);
    Task<List<ReservaDto>> ListarReservasAsync(int pasajeroId);
}

public interface IReservaService
{
    Task<List<ReservaDto>> ListarAsync(int? vueloId, int? pasajeroId, EstadoReserva? estado);
    Task<ReservaDto> ObtenerAsync(int id);
    Task<ReservaDto> CrearAsync(ReservaCrearDto dto);
    Task<ReservaDto> ConfirmarAsync(int id);
    Task<ReservaDto> CancelarAsync(int id);
}

public interface ITicketService
{
    Task<List<TicketDto>> ListarAsync(int? reservaId);
    Task<TicketDto> ObtenerPorCodigoAsync(string codigo);
    Task<TicketDto> EmitirAsync(int reservaId);
    //Validación de embarque que consume el aeropuerto: ¿el ticket es válido y está vigente?
    Task<TicketValidacionDto> ValidarAsync(string codigo);
}
