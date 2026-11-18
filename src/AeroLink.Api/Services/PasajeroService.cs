using AeroLink.Api.Dtos;
using AeroLink.Api.Exceptions;
using AeroLink.Api.Models;
using AeroLink.Api.Repositories;

namespace AeroLink.Api.Services;

public class PasajeroService : IPasajeroService
{
    private readonly IPasajeroRepository _pasajeros;
    private readonly IReservaRepository _reservas;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<PasajeroService> _logger;

    public PasajeroService(IPasajeroRepository pasajeros, IReservaRepository reservas, IUnitOfWork uow,
        ILogger<PasajeroService> logger)
    {
        _pasajeros = pasajeros;
        _reservas = reservas;
        _uow = uow;
        _logger = logger;
    }

    public async Task<List<PasajeroDto>> ListarAsync() =>
        (await _pasajeros.ListarAsync()).Select(PasajeroDto.Desde).ToList();

    public async Task<PasajeroDto> ObtenerAsync(int id) => PasajeroDto.Desde(await ObtenerEntidadAsync(id));

    public async Task<PasajeroDto> CrearAsync(PasajeroCrearDto dto)
    {
        var documento = dto.Documento!.Trim().ToUpperInvariant();
        if (await _pasajeros.ExisteDocumentoAsync(documento))
            throw new ConflictException("PASSENGER_DOCUMENT_DUPLICATED",
                $"Ya existe un pasajero con el documento {documento}.");

        var pasajero = new Pasajero
        {
            Nombre = dto.Nombre!.Trim(),
            Apellido = dto.Apellido!.Trim(),
            Documento = documento,
            Email = dto.Email!.Trim().ToLowerInvariant()
        };
        _pasajeros.Agregar(pasajero);
        await _uow.GuardarCambiosAsync();

        _logger.LogInformation("Pasajero {Id} registrado", pasajero.Id);
        return PasajeroDto.Desde(pasajero);
    }

    public async Task<List<ReservaDto>> ListarReservasAsync(int pasajeroId)
    {
        await ObtenerEntidadAsync(pasajeroId); // 404 si no existe
        return (await _reservas.ListarAsync(null, pasajeroId, null)).Select(ReservaDto.Desde).ToList();
    }

    private async Task<Pasajero> ObtenerEntidadAsync(int id) =>
        await _pasajeros.ObtenerAsync(id)
        ?? throw new NotFoundException("PASSENGER_NOT_FOUND", $"No existe el pasajero con id {id}.");
}
