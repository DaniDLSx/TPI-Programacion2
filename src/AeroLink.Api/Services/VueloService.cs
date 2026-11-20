using AeroLink.Api.Dtos;
using AeroLink.Api.Exceptions;
using AeroLink.Api.Integration;
using AeroLink.Api.Models;
using AeroLink.Api.Repositories;
using Microsoft.Extensions.Options;

namespace AeroLink.Api.Services;

public class VueloService : IVueloService
{

    private readonly IVueloRepository _vuelos;
    private readonly IUnitOfWork _uow;
    private readonly IAeropuertoClient _aeropuerto;
    private readonly AeropuertoOptions _opciones;
    private readonly ILogger<VueloService> _logger;

    public VueloService(IVueloRepository vuelos, IUnitOfWork uow, IAeropuertoClient aeropuerto,
        IOptions<AeropuertoOptions> opciones, ILogger<VueloService> logger)
    {
        _vuelos = vuelos;
        _uow = uow;
        _aeropuerto = aeropuerto;
        _opciones = opciones.Value;
        _logger = logger;
    }

    // ---------- Consultas ----------

    public async Task<List<VueloDto>> ListarAsync(EstadoVuelo? estado, string? origen, string? destino)
    {
        var vuelos = await _vuelos.ListarAsync(estado, Normalizar(origen), Normalizar(destino));
        return vuelos.Select(VueloDto.Desde).ToList();
    }

    public async Task<VueloDto> ObtenerAsync(int id) => VueloDto.Desde(await ObtenerEntidadAsync(id));

    // ---------- ABM ----------

    public async Task<VueloDto> CrearAsync(VueloCrearDto dto)
    {
        var codigo = Normalizar(dto.CodigoVuelo)!;
        if (await _vuelos.ExisteCodigoAsync(codigo))
            throw new ConflictException("FLIGHT_CODE_DUPLICATED", $"Ya existe un vuelo con el código {codigo}.");

        var vuelo = new Vuelo
        {
            CodigoVuelo = codigo,
            Origen = Normalizar(dto.Origen)!,
            Destino = Normalizar(dto.Destino)!,
            HorarioSalida = AUtc(dto.HorarioSalida!.Value),
            HorarioLlegada = AUtc(dto.HorarioLlegada!.Value),
            Aeronave = dto.Aeronave!.Trim(),
            Estado = EstadoVuelo.Programado,
            EstadoSlot = EstadoSlot.SinSlot
        };
        _vuelos.Agregar(vuelo);
        await _uow.GuardarCambiosAsync();

        _logger.LogInformation("Vuelo {Codigo} creado (id {Id})", vuelo.CodigoVuelo, vuelo.Id);
        return VueloDto.Desde(vuelo);
    }

    public async Task<VueloDto> ActualizarAsync(int id, VueloActualizarDto dto)
    {
        var vuelo = await ObtenerEntidadAsync(id);
        if (vuelo.Estado == EstadoVuelo.Cancelado)
            throw new ConflictException("FLIGHT_CANCELLED", "No se puede modificar un vuelo cancelado.");

        var origen = Normalizar(dto.Origen)!;
        var destino = Normalizar(dto.Destino)!;
        var salida = AUtc(dto.HorarioSalida!.Value);
        var llegada = AUtc(dto.HorarioLlegada!.Value);

        var cambiaItinerario = vuelo.Origen != origen || vuelo.Destino != destino
                               || vuelo.HorarioSalida != salida || vuelo.HorarioLlegada != llegada;
        // El slot se otorgó para un itinerario concreto: si cambia, ese slot deja de ser válido.
        if (cambiaItinerario && (vuelo.Estado == EstadoVuelo.Confirmado || SlotVigente(vuelo)))
            throw new ConflictException("FLIGHT_SLOT_LOCKED",
                "El vuelo tiene un slot solicitado u otorgado: no se puede cambiar origen, destino ni horarios. " +
                "Cancele el vuelo y cree uno nuevo.");

        vuelo.Origen = origen;
        vuelo.Destino = destino;
        vuelo.HorarioSalida = salida;
        vuelo.HorarioLlegada = llegada;
        vuelo.Aeronave = dto.Aeronave!.Trim();
        await _uow.GuardarCambiosAsync();

        _logger.LogInformation("Vuelo {Codigo} modificado", vuelo.CodigoVuelo);
        return VueloDto.Desde(vuelo);
    }

    // ---------- Integración con el aeropuerto ----------

    public async Task<VueloDto> SolicitarSlotAsync(int id, SolicitarSlotDto dto)
    {
        var vuelo = await ObtenerEntidadAsync(id);
        if (vuelo.Estado == EstadoVuelo.Cancelado)
            throw new ConflictException("FLIGHT_CANCELLED", "No se puede solicitar un slot para un vuelo cancelado.");
        if (vuelo.Estado == EstadoVuelo.Confirmado)
            throw new ConflictException("FLIGHT_ALREADY_CONFIRMED", "El vuelo ya está confirmado y tiene su slot.");
        if (SlotVigente(vuelo))
            throw new ConflictException("SLOT_ALREADY_REQUESTED",
                "El vuelo ya tiene un slot solicitado u otorgado.", new { slotId = vuelo.SlotId });

        var pista = dto.Pista!.Value;
        var gate = dto.Gate!.Trim().ToUpperInvariant();

        // Contrato PortHub: se consulta la disponibilidad de pista y horario antes de pedir el slot.
        if (!await _aeropuerto.ConsultarDisponibilidadAsync(pista, vuelo.HorarioSalida))
            throw new ConflictException("SLOT_NOT_AVAILABLE",
                $"El aeropuerto informa que la pista {pista} no está disponible en el horario del vuelo.",
                new { pista, horario = vuelo.HorarioSalida });

        var slot = await _aeropuerto.SolicitarSlotAsync(new SolicitudSlot(
            vuelo.CodigoVuelo, _opciones.CodigoAerolinea, vuelo.HorarioSalida, pista, gate));

        vuelo.Pista = pista;
        vuelo.Gate = gate;
        vuelo.SlotId = slot.SlotId;
        vuelo.EstadoSlot = slot.Estado switch
        {
            EstadoSlotAeropuerto.Otorgado => EstadoSlot.Otorgado,
            EstadoSlotAeropuerto.Rechazado => EstadoSlot.Rechazado,
            _ => EstadoSlot.Solicitado
        };
        await _uow.GuardarCambiosAsync();

        _logger.LogInformation("Slot {SlotId} solicitado para el vuelo {Codigo}: {Estado}",
            slot.SlotId, vuelo.CodigoVuelo, vuelo.EstadoSlot);
        return VueloDto.Desde(vuelo);
    }

    public async Task<VueloDto> ConfirmarAsync(int id)
    {
        var vuelo = await ObtenerEntidadAsync(id);
        if (vuelo.Estado == EstadoVuelo.Cancelado)
            throw new ConflictException("FLIGHT_CANCELLED", "No se puede confirmar un vuelo cancelado.");
        if (vuelo.Estado == EstadoVuelo.Confirmado)
            return VueloDto.Desde(vuelo); // idempotente

        if (string.IsNullOrEmpty(vuelo.SlotId) || vuelo.EstadoSlot is EstadoSlot.SinSlot or EstadoSlot.Liberado)
            throw new ConflictException("SLOT_NOT_REQUESTED",
                "El vuelo no tiene slot: primero solicítelo con POST /api/vuelos/{id}/solicitar-slot.");
        if (vuelo.EstadoSlot == EstadoSlot.Rechazado)
            throw new ConflictException("SLOT_REJECTED",
                "El aeropuerto rechazó el slot: solicite uno nuevo antes de confirmar.",
                new { slotId = vuelo.SlotId });

        await ConfirmarValidandoSlotAsync(vuelo);
        return VueloDto.Desde(vuelo);
    }

    // Regla central del TP: un vuelo solo pasa a Confirmado si el aeropuerto informa el slot como otorgado.
    // Se consulta SIEMPRE a la API del aeropuerto antes de confirmar, tanto desde /confirmar como al
    // recibir el callback (así un callback falso o adelantado no puede confirmar un vuelo sin slot).

    private async Task ConfirmarValidandoSlotAsync(Vuelo vuelo)
    {
        var slot = await _aeropuerto.ObtenerSlotPorVueloAsync(vuelo.CodigoVuelo);
        if (slot is null || slot.Estado != EstadoSlotAeropuerto.Otorgado)
        {
            if (slot?.Estado == EstadoSlotAeropuerto.Rechazado)
            {
                vuelo.EstadoSlot = EstadoSlot.Rechazado;
                if (vuelo.Estado == EstadoVuelo.Confirmado) vuelo.Estado = EstadoVuelo.Programado;
                await _uow.GuardarCambiosAsync();
            }
            throw new ConflictException("SLOT_NOT_GRANTED",
                "El aeropuerto no informa el slot como otorgado: el vuelo no puede confirmarse.",
                new { slotId = vuelo.SlotId, estadoAeropuerto = slot?.Estado.ToString() ?? "Inexistente" });
        }

        vuelo.EstadoSlot = EstadoSlot.Otorgado;
        vuelo.Estado = EstadoVuelo.Confirmado;
        await _uow.GuardarCambiosAsync();

        _logger.LogInformation("Vuelo {Codigo} confirmado con slot {SlotId}", vuelo.CodigoVuelo, vuelo.SlotId);
    }

    public async Task<VueloDto> RecibirCallbackSlotAsync(CallbackSlotDto dto)
    {
        // El aeropuerto puede identificar el vuelo por slotId o por codigoVuelo.
        var vuelo = (!string.IsNullOrWhiteSpace(dto.SlotId)
                        ? await _vuelos.ObtenerPorSlotIdAsync(dto.SlotId.Trim())
                        : await _vuelos.ObtenerPorCodigoAsync(Normalizar(dto.CodigoVuelo)!))
                    ?? throw new NotFoundException("SLOT_NOT_FOUND",
                        $"Ningún vuelo tiene el slot {(string.IsNullOrWhiteSpace(dto.SlotId) ? dto.CodigoVuelo : dto.SlotId)}.");

        if (!string.IsNullOrWhiteSpace(dto.CodigoVuelo) &&
            !string.Equals(dto.CodigoVuelo.Trim(), vuelo.CodigoVuelo, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("SLOT_FLIGHT_MISMATCH",
                $"El slot {dto.SlotId} pertenece al vuelo {vuelo.CodigoVuelo}, no a {dto.CodigoVuelo}.");

        if (vuelo.Estado == EstadoVuelo.Cancelado)
            throw new ConflictException("FLIGHT_CANCELLED", "El vuelo está cancelado: el callback se ignora.");

        if (dto.Estado!.Trim().ToLowerInvariant() is "otorgado" or "confirmado")
        {
            // El callback avisa, pero la confirmación se valida contra la API del aeropuerto (502 si no responde:
            // el aeropuerto puede reintentar el callback).
            await ConfirmarValidandoSlotAsync(vuelo);
        }
        else
        {
            vuelo.EstadoSlot = EstadoSlot.Rechazado;
            if (vuelo.Estado == EstadoVuelo.Confirmado) vuelo.Estado = EstadoVuelo.Programado;
            await _uow.GuardarCambiosAsync();
        }

        _logger.LogInformation("Callback del aeropuerto: slot {SlotId} {Estado}, vuelo {Codigo} ahora {EstadoVuelo}",
            dto.SlotId, dto.Estado, vuelo.CodigoVuelo, vuelo.Estado);
        return VueloDto.Desde(vuelo);
    }

    // ---------- Cancelación ----------

    public async Task<CancelacionVueloDto> CancelarAsync(int id)
    {
        var vuelo = await _vuelos.ObtenerConReservasAsync(id)
                    ?? throw new NotFoundException("FLIGHT_NOT_FOUND", $"No existe el vuelo con id {id}.");
        if (vuelo.Estado == EstadoVuelo.Cancelado)
            throw new ConflictException("FLIGHT_ALREADY_CANCELLED", "El vuelo ya está cancelado.");

        // 1) Avisar al aeropuerto. Si falla (502) no se modifica nada y se puede reintentar.
        var liberaSlot = !string.IsNullOrEmpty(vuelo.SlotId) && SlotVigente(vuelo);
        if (liberaSlot)
            await _aeropuerto.LiberarSlotAsync(vuelo.CodigoVuelo);

        // 2) Anular en cascada reservas y tickets.
        int reservas = 0, tickets = 0;
        foreach (var reserva in vuelo.Reservas.Where(r => r.Estado != EstadoReserva.Anulada))
        {
            reserva.Estado = EstadoReserva.Anulada;
            reservas++;
            if (reserva.Ticket is { Estado: EstadoTicket.Emitido } ticket)
            {
                ticket.Estado = EstadoTicket.Anulado;
                tickets++;
            }
        }

        vuelo.Estado = EstadoVuelo.Cancelado;
        if (liberaSlot) vuelo.EstadoSlot = EstadoSlot.Liberado;
        await _uow.GuardarCambiosAsync();

        _logger.LogInformation("Vuelo {Codigo} cancelado: {Reservas} reservas y {Tickets} tickets anulados, slot liberado: {Slot}",
            vuelo.CodigoVuelo, reservas, tickets, liberaSlot);
        return new CancelacionVueloDto
        {
            Vuelo = VueloDto.Desde(vuelo),
            ReservasAnuladas = reservas,
            TicketsAnulados = tickets,
            SlotLiberado = liberaSlot
        };
    }

    // ---------- Auxiliares ----------

    private async Task<Vuelo> ObtenerEntidadAsync(int id) =>
        await _vuelos.ObtenerAsync(id)
        ?? throw new NotFoundException("FLIGHT_NOT_FOUND", $"No existe el vuelo con id {id}.");

    private static bool SlotVigente(Vuelo v) => v.EstadoSlot is EstadoSlot.Solicitado or EstadoSlot.Otorgado;

    private static string? Normalizar(string? texto) => texto?.Trim().ToUpperInvariant();

    private static DateTime AUtc(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => fecha,
        DateTimeKind.Local => fecha.ToUniversalTime(),
        _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc) // sin zona: se interpreta como UTC
    };
}
