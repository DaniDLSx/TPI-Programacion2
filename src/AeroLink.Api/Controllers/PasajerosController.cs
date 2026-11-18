using AeroLink.Api.Dtos;
using AeroLink.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeroLink.Api.Controllers;

[ApiController]
[Route("api/pasajeros")]
[Produces("application/json")]
public class PasajerosController : ControllerBase
{
    private readonly IPasajeroService _servicio;
    public PasajerosController(IPasajeroService servicio) => _servicio = servicio;

    //Lista todos los pasajeros.
    [HttpGet]
    public async Task<ActionResult<List<PasajeroDto>>> Listar() => Ok(await _servicio.ListarAsync());

    //Obtiene un pasajero por id.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PasajeroDto>> Obtener(int id) => Ok(await _servicio.ObtenerAsync(id));

    //Lista las reservas de un pasajero.
    [HttpGet("{id:int}/reservas")]
    public async Task<ActionResult<List<ReservaDto>>> ListarReservas(int id) =>
        Ok(await _servicio.ListarReservasAsync(id));

    //Registra un pasajero (el documento debe ser único).
    [HttpPost]
    public async Task<ActionResult<PasajeroDto>> Crear([FromBody] PasajeroCrearDto dto)
    {
        var creado = await _servicio.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }
}
