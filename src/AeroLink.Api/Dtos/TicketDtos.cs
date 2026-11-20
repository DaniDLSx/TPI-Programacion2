using System.ComponentModel.DataAnnotations;
using AeroLink.Api.Models;

namespace AeroLink.Api.Dtos;

public class TicketCrearDto
{
    // <example>1</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [Range(1, int.MaxValue, ErrorMessage = "El campo {0} debe ser un id válido.")]
    public int? ReservaId { get; set; }
}

public class TicketDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int ReservaId { get; set; }
    public string CodigoVuelo { get; set; } = string.Empty;
    public string Pasajero { get; set; } = string.Empty;
    public string Asiento { get; set; } = string.Empty;
    public EstadoTicket Estado { get; set; }
    public DateTime FechaEmision { get; set; }

    public static TicketDto Desde(Ticket t) => new()
    {
        Id = t.Id,
        Codigo = t.Codigo,
        ReservaId = t.ReservaId,
        CodigoVuelo = t.Reserva?.Vuelo?.CodigoVuelo ?? string.Empty,
        Pasajero = t.Reserva?.Pasajero is null ? string.Empty : $"{t.Reserva.Pasajero.Nombre} {t.Reserva.Pasajero.Apellido}",
        Asiento = t.Reserva?.Asiento ?? string.Empty,
        Estado = t.Estado,
        FechaEmision = t.FechaEmision
    };
}

//Respuesta de GET /api/tickets/{codigo}/validacion (contrato con PortHub, usado en el embarque).
public class TicketValidacionDto
{
    public bool EsValido { get; set; }
    public string CodigoTicket { get; set; } = string.Empty;
    public string? CodigoVuelo { get; set; }
    public string? Pasajero { get; set; }
    /// <summary>Por qué no es válido (null si lo es).</summary>
    public string? Motivo { get; set; }
}
