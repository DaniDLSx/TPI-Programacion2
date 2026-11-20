using System.Collections.Concurrent;
using AeroLink.Api.Exceptions;

namespace AeroLink.Api.Integration;

// Cliente en memoria para desarrollo (Aeropuerto:Modo = "Simulado"). NO es una simulación del
// aeropuerto completo: solo permite ejecutar AeroLink sin PortHub. Una solicitud queda
// PENDIENTE ("reservado") y pasa a OTORGADO ("confirmado") la primera vez que AeroLink la consulta.
// Una pista ocupada a esa hora por otro vuelo se informa como SLOT_OCCUPIED.
public class AeropuertoSimuladoClient : IAeropuertoClient
{
    private record Registro(string SlotId, int Pista, string Gate, DateTime Horario, EstadoSlotAeropuerto Estado);

    private readonly ConcurrentDictionary<string, Registro> _porVuelo = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _candado = new();

    public Task<bool> ConsultarDisponibilidadAsync(int pista, DateTime horario, CancellationToken ct = default)
    {
        lock (_candado) return Task.FromResult(!Ocupada(pista, horario, null));
    }

    public Task<SlotAeropuerto> SolicitarSlotAsync(SolicitudSlot s, CancellationToken ct = default)
    {
        lock (_candado)
        {
            if (Ocupada(s.Pista, s.HorarioProgramado, s.CodigoVuelo))
                throw new ConflictException("SLOT_OCCUPIED",
                    $"La pista {s.Pista} no se encuentra disponible en el horario solicitado.");

            var id = Guid.NewGuid().ToString();
            _porVuelo[s.CodigoVuelo] = new Registro(id, s.Pista, s.Gate, s.HorarioProgramado, EstadoSlotAeropuerto.Pendiente);
            return Task.FromResult(new SlotAeropuerto(id, s.CodigoVuelo, s.Pista, s.Gate, EstadoSlotAeropuerto.Pendiente));
        }
    }

    public Task<SlotAeropuerto?> ObtenerSlotPorVueloAsync(string codigoVuelo, CancellationToken ct = default)
    {
        lock (_candado)
        {
            if (!_porVuelo.TryGetValue(codigoVuelo, out var r)) return Task.FromResult<SlotAeropuerto?>(null);
            if (r.Estado == EstadoSlotAeropuerto.Pendiente)
            {
                r = r with { Estado = EstadoSlotAeropuerto.Otorgado };
                _porVuelo[codigoVuelo] = r;
            }
            return Task.FromResult<SlotAeropuerto?>(new SlotAeropuerto(r.SlotId, codigoVuelo, r.Pista, r.Gate, r.Estado));
        }
    }

    public Task LiberarSlotAsync(string codigoVuelo, CancellationToken ct = default)
    {
        lock (_candado) _porVuelo.TryRemove(codigoVuelo, out _);
        return Task.CompletedTask;
    }

    private bool Ocupada(int pista, DateTime horario, string? codigoVueloPropio) =>
        _porVuelo.Any(kv => kv.Value.Pista == pista && kv.Value.Horario == horario &&
                            !string.Equals(kv.Key, codigoVueloPropio, StringComparison.OrdinalIgnoreCase));
}
