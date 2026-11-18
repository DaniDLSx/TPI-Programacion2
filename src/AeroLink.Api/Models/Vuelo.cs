namespace AeroLink.Api.Models;

public class Vuelo
{
    public int Id { get; set; }
    public string CodigoVuelo { get; set; } = string.Empty;
    public string Origen { get; set; } = string.Empty;
    public string Destino { get; set; } = string.Empty;
    public DateTime HorarioSalida { get; set; }
    public DateTime HorarioLlegada { get; set; }
    public string Aeronave { get; set; } = string.Empty;
    public EstadoVuelo Estado { get; set; } = EstadoVuelo.Programado;

    //Identificador del slot que devolvió el aeropuerto (null si todavía no se solicitó).
    public string? SlotId { get; set; }
    //Pista y gate pedidos al aeropuerto al solicitar el slot (null mientras no se solicitó).
    public int? Pista { get; set; }
    public string? Gate { get; set; }
    public EstadoSlot EstadoSlot { get; set; } = EstadoSlot.SinSlot;

    public List<Reserva> Reservas { get; set; } = new();
}
