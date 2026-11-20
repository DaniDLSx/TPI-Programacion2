using System.Security.Cryptography;
using AeroLink.Api.Dtos;
using AeroLink.Api.Exceptions;
using AeroLink.Api.Models;
using AeroLink.Api.Repositories;

namespace AeroLink.Api.Services;

public class TicketService : ITicketService
{
    // Sin caracteres ambiguos (0/O, 1/I/L) para que el código sea fácil de leer en un mostrador.
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int LongitudCodigo = 10;
    private const int MaxIntentos = 10;

    private readonly ITicketRepository _tickets;
    private readonly IReservaRepository _reservas;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TicketService> _logger;

    public TicketService(ITicketRepository tickets, IReservaRepository reservas, IUnitOfWork uow,
        ILogger<TicketService> logger)
    {
        _tickets = tickets;
        _reservas = reservas;
        _uow = uow;
        _logger = logger;
    }

    public async Task<List<TicketDto>> ListarAsync(int? reservaId) =>
        (await _tickets.ListarAsync(reservaId)).Select(TicketDto.Desde).ToList();

    public async Task<TicketDto> ObtenerPorCodigoAsync(string codigo)
    {
        var ticket = await _tickets.ObtenerPorCodigoAsync(codigo.Trim().ToUpperInvariant())
                     ?? throw new NotFoundException("TICKET_NOT_FOUND", $"No existe el ticket {codigo}.");
        return TicketDto.Desde(ticket);
    }

    public async Task<TicketValidacionDto> ValidarAsync(string codigo)
    {
        var buscado = codigo.Trim().ToUpperInvariant();
        var ticket = await _tickets.ObtenerPorCodigoAsync(buscado);
        if (ticket is null)
            return new TicketValidacionDto { EsValido = false, CodigoTicket = buscado, Motivo = "El ticket no existe." };

        var reserva = ticket.Reserva;
        var vuelo = reserva?.Vuelo;
        string? motivo = null;
        if (ticket.Estado != EstadoTicket.Emitido) motivo = "El ticket fue anulado.";
        else if (reserva is null || reserva.Estado != EstadoReserva.Confirmada) motivo = "La reserva no está confirmada.";
        else if (vuelo is null || vuelo.Estado == EstadoVuelo.Cancelado) motivo = "El vuelo está cancelado.";
        else if (vuelo.HorarioLlegada < DateTime.UtcNow) motivo = "El vuelo ya finalizó: el ticket no está vigente.";

        return new TicketValidacionDto
        {
            EsValido = motivo is null,
            CodigoTicket = ticket.Codigo,
            CodigoVuelo = vuelo?.CodigoVuelo,
            Pasajero = reserva?.Pasajero is null ? null : $"{reserva.Pasajero.Nombre} {reserva.Pasajero.Apellido}",
            Motivo = motivo
        };
    }

    public async Task<TicketDto> EmitirAsync(int reservaId)
    {
        var reserva = await _reservas.ObtenerAsync(reservaId)
                      ?? throw new NotFoundException("RESERVATION_NOT_FOUND", $"No existe la reserva con id {reservaId}.");

        if (reserva.Estado != EstadoReserva.Confirmada)
            throw new ConflictException("RESERVATION_NOT_CONFIRMED",
                "Solo se puede emitir un ticket para una reserva confirmada.",
                new { estadoReserva = reserva.Estado.ToString() });
        if (reserva.Ticket is not null)
            throw new ConflictException("TICKET_ALREADY_ISSUED",
                "La reserva ya tiene un ticket emitido.", new { codigo = reserva.Ticket.Codigo });

        var ticket = new Ticket
        {
            Codigo = await GenerarCodigoUnicoAsync(),
            Reserva = reserva,
            ReservaId = reserva.Id,
            FechaEmision = DateTime.UtcNow,
            Estado = EstadoTicket.Emitido
        };
        _tickets.Agregar(ticket);
        reserva.Ticket = ticket;
        await _uow.GuardarCambiosAsync();

        _logger.LogInformation("Ticket {Codigo} emitido para la reserva {Reserva}", ticket.Codigo, reserva.Id);
        return TicketDto.Desde(ticket);
    }

    /// <summary>Código aleatorio criptográficamente seguro; se verifica contra la base y el índice único es la garantía final.</summary>
    private async Task<string> GenerarCodigoUnicoAsync()
    {
        for (var intento = 0; intento < MaxIntentos; intento++)
        {
            var chars = new char[LongitudCodigo];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
            var codigo = "AL-" + new string(chars);

            if (!await _tickets.ExisteCodigoAsync(codigo)) return codigo;
        }
        throw new InvalidOperationException("No se pudo generar un código de ticket único.");
    }
}
