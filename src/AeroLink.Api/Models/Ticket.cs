namespace AeroLink.Api.Models;

public class Ticket
{
    public int Id { get; set; }
    /// <summary>Código único del ticket electrónico (ej: AL-7K2M9XQ4TB).</summary>
    public string Codigo { get; set; } = string.Empty;
    public int ReservaId { get; set; }
    public Reserva? Reserva { get; set; }
    public DateTime FechaEmision { get; set; }
    public EstadoTicket Estado { get; set; } = EstadoTicket.Emitido;
}
