namespace AeroLink.Api.Models;

public class Pasajero
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; //Es lo mismo que poner ""
    public string Apellido { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public List<Reserva> Reservas { get; set; } = new();
}
