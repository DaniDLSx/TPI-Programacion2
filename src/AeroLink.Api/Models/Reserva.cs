namespace AeroLink.Api.Models;

public class Reserva
{
    public int Id { get; set; }
    public int PasajeroId { get; set; }
    public Pasajero? Pasajero { get; set; }
    public int VueloId { get; set; }
    public Vuelo? Vuelo { get; set; }
    public string Asiento { get; set; } = string.Empty;
    public EstadoReserva Estado { get; set; } = EstadoReserva.Pendiente;
    public DateTime FechaEmision { get; set; }

    public Ticket? Ticket { get; set; }
}
