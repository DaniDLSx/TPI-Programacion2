using AeroLink.Api.Dtos;
using AeroLink.Api.Security;
using AeroLink.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeroLink.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _servicio;
    public TicketsController(ITicketService servicio) => _servicio = servicio;

    //Lista tickets (filtro opcional por reserva).
    [HttpGet]
    public async Task<ActionResult<List<TicketDto>>> Listar([FromQuery] int? reservaId) =>
        Ok(await _servicio.ListarAsync(reservaId));

    //Obtiene un ticket por su código único.
    [HttpGet("{codigo}")]
    public async Task<ActionResult<TicketDto>> Obtener(string codigo) =>
        Ok(await _servicio.ObtenerPorCodigoAsync(codigo));

    //Validación de embarque (la consume el aeropuerto): informa si el ticket es válido y vigente.
    //Siempre responde 200; si no es válido, esValido = false y motivo explica por qué. Requiere API Key.
    [HttpGet("{codigo}/validacion")]
    [RequiereApiKey]
    public async Task<ActionResult<TicketValidacionDto>> Validar(string codigo) =>
        Ok(await _servicio.ValidarAsync(codigo));

    //Emite el ticket electrónico de una reserva confirmada. Requiere API Key.
    [HttpPost]
    [RequiereApiKey]
    public async Task<ActionResult<TicketDto>> Emitir([FromBody] TicketCrearDto dto)
    {
        var ticket = await _servicio.EmitirAsync(dto.ReservaId!.Value);
        return CreatedAtAction(nameof(Obtener), new { codigo = ticket.Codigo }, ticket);
    }
}
