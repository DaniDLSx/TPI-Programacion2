using AeroLink.Api.Dtos;
using AeroLink.Api.Models;
using AeroLink.Api.Security;
using AeroLink.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeroLink.Api.Controllers;

[ApiController]
[Route("api/vuelos")]
[Produces("application/json")]
public class VuelosController : ControllerBase
{
    private readonly IVueloService _servicio;
    public VuelosController(IVueloService servicio) => _servicio = servicio;

    //Lista los vuelos (filtros opcionales por estado, origen y destino).
    [HttpGet]
    public async Task<ActionResult<List<VueloDto>>> Listar(
        [FromQuery] EstadoVuelo? estado, [FromQuery] string? origen, [FromQuery] string? destino)
        => Ok(await _servicio.ListarAsync(estado, origen, destino));

    //Consulta consolidada: vuelos con estado de slot (aerolínea y aeropuerto) y reservas asociadas.
    [HttpGet("consolidado")]
    public async Task<ActionResult<List<VueloConsolidadoDto>>> Consolidado([FromQuery] EstadoVuelo? estado)
        => Ok(await _servicio.ConsolidadoAsync(estado));

    //Obtiene un vuelo por id.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<VueloDto>> Obtener(int id) => Ok(await _servicio.ObtenerAsync(id));

    //Registra un vuelo nuevo (queda en estado Programado, sin slot).
    [HttpPost]
    public async Task<ActionResult<VueloDto>> Crear([FromBody] VueloCrearDto dto)
    {
        var creado = await _servicio.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    //Modifica origen, destino, horarios y aeronave (no el código ni el estado).
    [HttpPut("{id:int}")]
    public async Task<ActionResult<VueloDto>> Actualizar(int id, [FromBody] VueloActualizarDto dto)
        => Ok(await _servicio.ActualizarAsync(id, dto));

    //Solicita al aeropuerto la asignación de un slot (pista + gate). Requiere API Key.
    [HttpPost("{id:int}/solicitar-slot")]
    [RequiereApiKey]
    public async Task<ActionResult<VueloDto>> SolicitarSlot(int id, [FromBody] SolicitarSlotDto dto)
        => Ok(await _servicio.SolicitarSlotAsync(id, dto));

    //Confirma el vuelo solo si el aeropuerto informa el slot como otorgado. Requiere API Key.
    [HttpPost("{id:int}/confirmar")]
    [RequiereApiKey]
    public async Task<ActionResult<VueloDto>> Confirmar(int id) => Ok(await _servicio.ConfirmarAsync(id));

    //Cancela el vuelo: libera el slot en el aeropuerto y anula reservas y tickets. Requiere API Key.
    [HttpPost("{id:int}/cancelar")]
    [RequiereApiKey]
    public async Task<ActionResult<CancelacionVueloDto>> Cancelar(int id) => Ok(await _servicio.CancelarAsync(id));
}
