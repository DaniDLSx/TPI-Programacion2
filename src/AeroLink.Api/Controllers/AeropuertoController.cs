using AeroLink.Api.Dtos;
using AeroLink.Api.Security;
using AeroLink.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeroLink.Api.Controllers;

//Endpoints que consume el aeropuerto.
[ApiController]
[Route("api/aeropuerto")]
[Produces("application/json")]
[RequiereApiKey]
public class AeropuertoController : ControllerBase
{
    private readonly IVueloService _servicio;
    public AeropuertoController(IVueloService servicio) => _servicio = servicio;

    // Callback del aeropuerto: informa si el slot fue OTORGADO o RECHAZADO.
    // Si fue otorgado, el vuelo pasa a Confirmado. Requiere API Key.
    [HttpPost("callbacks/slots")]
    public async Task<ActionResult<VueloDto>> CallbackSlot([FromBody] CallbackSlotDto dto)
        => Ok(await _servicio.RecibirCallbackSlotAsync(dto));
}
