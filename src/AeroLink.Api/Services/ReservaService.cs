using AeroLink.Api.Dtos;
using AeroLink.Api.Exceptions;
using AeroLink.Api.Models;
using AeroLink.Api.Repositories;

namespace AeroLink.Api.Services;

public class ReservaService : IReservaService
{
    private readonly IReservaRepository _reservas;
    private readonly IVueloRepository _vuelos;
    private readonly IPasajeroRepository _pasajeros;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ReservaService> _logger;

    public ReservaService(IReservaRepository reservas, IVueloRepository vuelos, IPasajeroRepository pasajeros,
        IUnitOfWork uow, ILogger<ReservaService> logger)
    {
        _reservas = reservas;
        _vuelos = vuelos;
        _pasajeros = pasajeros;
        _uow = uow;
        _logger = logger;
    }

    public async Task<List<ReservaDto>> ListarAsync(int? vueloId, int? pasajeroId, EstadoReserva? estado) =>
        (await _reservas.ListarAsync(vueloId, pasajeroId, estado)).Select(ReservaDto.Desde).ToList();

    public async Task<ReservaDto> ObtenerAsync(int id) => ReservaDto.Desde(await ObtenerEntidadAsync(id));

    public async Task<ReservaDto> CrearAsync(ReservaCrearDto dto)
    {
        var vuelo = await _vuelos.ObtenerAsync(dto.VueloId!.Value)
                    ?? throw new NotFoundException("FLIGHT_NOT_FOUND", $"No existe el vuelo con id {dto.VueloId}.");
        var pasajero = await _pasajeros.ObtenerAsync(dto.PasajeroId!.Value)
                       ?? throw new NotFoundException("PASSENGER_NOT_FOUND", $"No existe el pasajero con id {dto.PasajeroId}.");

        if (vuelo.Estado == EstadoVuelo.Cancelado)
            throw new ConflictException("FLIGHT_CANCELLED", "No se pueden crear reservas en un vuelo cancelado.");

        var asiento = dto.Asiento!.Trim().ToUpperInvariant();
        if (await _reservas.AsientoOcupadoAsync(vuelo.Id, asiento))
            throw new ConflictException("SEAT_TAKEN", $"El asiento {asiento} ya está reservado en el vuelo {vuelo.CodigoVuelo}.");
        if (await _reservas.ExisteReservaActivaAsync(vuelo.Id, pasajero.Id))
            throw new ConflictException("RESERVATION_DUPLICATED",
                $"El pasajero ya tiene una reserva activa en el vuelo {vuelo.CodigoVuelo}.");

        var reserva = new Reserva
        {
            Vuelo = vuelo, VueloId = vuelo.Id,
            Pasajero = pasajero, PasajeroId = pasajero.Id,
            Asiento = asiento,
            Estado = EstadoReserva.Pendiente,
            FechaEmision = DateTime.UtcNow
        };
        _reservas.Agregar(reserva);
        await _uow.GuardarCambiosAsync();

        _logger.LogInformation("Reserva {Id} creada: pasajero {Pasajero}, vuelo {Vuelo}, asiento {Asiento}",
            reserva.Id, pasajero.Id, vuelo.CodigoVuelo, asiento);
        return ReservaDto.Desde(reserva);
    }

    public async Task<ReservaDto> ConfirmarAsync(int id)
    {
        var reserva = await ObtenerEntidadAsync(id);
        if (reserva.Estado == EstadoReserva.Confirmada) return ReservaDto.Desde(reserva); // idempotente
        if (reserva.Estado == EstadoReserva.Anulada)
            throw new ConflictException("RESERVATION_CANCELLED", "No se puede confirmar una reserva anulada.");
        if (reserva.Vuelo?.Estado == EstadoVuelo.Cancelado)
            throw new ConflictException("FLIGHT_CANCELLED", "El vuelo está cancelado.");

        reserva.Estado = EstadoReserva.Confirmada;
        await _uow.GuardarCambiosAsync();

        _logger.LogInformation("Reserva {Id} confirmada", reserva.Id);
        return ReservaDto.Desde(reserva);
    }

    public async Task<ReservaDto> CancelarAsync(int id)
    {
        var reserva = await ObtenerEntidadAsync(id);
        if (reserva.Estado == EstadoReserva.Anulada)
            throw new ConflictException("RESERVATION_ALREADY_CANCELLED", "La reserva ya está anulada.");

        reserva.Estado = EstadoReserva.Anulada;
        if (reserva.Ticket is { Estado: EstadoTicket.Emitido } ticket)
            ticket.Estado = EstadoTicket.Anulado;
        await _uow.GuardarCambiosAsync();

        _logger.LogInformation("Reserva {Id} anulada", reserva.Id);
        return ReservaDto.Desde(reserva);
    }

    private async Task<Reserva> ObtenerEntidadAsync(int id) =>
        await _reservas.ObtenerAsync(id)
        ?? throw new NotFoundException("RESERVATION_NOT_FOUND", $"No existe la reserva con id {id}.");
}
