using System.ComponentModel.DataAnnotations;
using AeroLink.Api.Models;

namespace AeroLink.Api.Dtos;

public class ReservaCrearDto
{
    // <example>1</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [Range(1, int.MaxValue, ErrorMessage = "El campo {0} debe ser un id válido.")]
    public int? PasajeroId { get; set; }

    // <example>1</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [Range(1, int.MaxValue, ErrorMessage = "El campo {0} debe ser un id válido.")]
    public int? VueloId { get; set; }

    // <example>12A</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [RegularExpression(@"^([1-9]|[1-5][0-9])[A-Fa-f]$",
        ErrorMessage = "El campo {0} debe ser fila (1-59) + letra A-F (ej: 12A).")]
    public string? Asiento { get; set; }
}

public class ReservaDto
{
    public int Id { get; set; }
    public int PasajeroId { get; set; }
    public string Pasajero { get; set; } = string.Empty;
    public int VueloId { get; set; }
    public string CodigoVuelo { get; set; } = string.Empty;
    public string Asiento { get; set; } = string.Empty;
    public EstadoReserva Estado { get; set; }
    public DateTime FechaEmision { get; set; }
    public string? TicketCodigo { get; set; }

    public static ReservaDto Desde(Reserva r) => new()
    {
        Id = r.Id,
        PasajeroId = r.PasajeroId,
        Pasajero = r.Pasajero is null ? string.Empty : $"{r.Pasajero.Nombre} {r.Pasajero.Apellido}",
        VueloId = r.VueloId,
        CodigoVuelo = r.Vuelo?.CodigoVuelo ?? string.Empty,
        Asiento = r.Asiento,
        Estado = r.Estado,
        FechaEmision = r.FechaEmision,
        TicketCodigo = r.Ticket?.Codigo
    };
}
