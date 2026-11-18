using AeroLink.Api.Dtos;
using AeroLink.Api.Models;
using AeroLink.Api.Security;
using AeroLink.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeroLink.Api.Controllers;

[ApiController]
[Route("api/reservas")]
[Produces("application/json")]
public class ReservasController : ControllerBase
{
    private readonly IReservaService _servicio;
    public ReservasController(IReservaService servicio) => _servicio = servicio;

    //Lista reservas (filtros opcionales por vuelo, pasajero y estado).
    [HttpGet]
    public async Task<ActionResult<List<ReservaDto>>> Listar(
        [FromQuery] int? vueloId, [FromQuery] int? pasajeroId, [FromQuery] EstadoReserva? estado)
        => Ok(await _servicio.ListarAsync(vueloId, pasajeroId, estado));

    //Obtiene una reserva por id.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservaDto>> Obtener(int id) => Ok(await _servicio.ObtenerAsync(id));

    //Crea una reserva (queda Pendiente) vinculando un pasajero con un vuelo y un asiento.
    [HttpPost]
    public async Task<ActionResult<ReservaDto>> Crear([FromBody] ReservaCrearDto dto)
    {
        var creada = await _servicio.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    //Confirma una reserva pendiente.
    [HttpPost("{id:int}/confirmar")]
    public async Task<ActionResult<ReservaDto>> Confirmar(int id) => Ok(await _servicio.ConfirmarAsync(id));

    //Anula una reserva (y su ticket, si lo tiene). Requiere API Key.
    [HttpPost("{id:int}/cancelar")]
    [RequiereApiKey]
    public async Task<ActionResult<ReservaDto>> Cancelar(int id) => Ok(await _servicio.CancelarAsync(id));
}
