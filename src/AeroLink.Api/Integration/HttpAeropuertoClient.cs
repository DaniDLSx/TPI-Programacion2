using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AeroLink.Api.Exceptions;

namespace AeroLink.Api.Integration;

// Cliente HTTP de PortHub (aeropuerto), según el "Contrato de Integración API - PortHub y AeroLink":
//   GET  /api/slots/disponibilidad?pista=1&amp;horario=2026-11-15T14:30:00Z  -> { disponible, pista, horario }
//   POST /api/slots/solicitudes   { codigoVuelo, codigoAerolinea, horarioProgramado, pista, gate }
//                                 -> 201 { slotId, codigoVuelo, pista, gate, estado:"reservado" }
//   POST /api/slots/cancelaciones { codigoVuelo } -> 200 { mensaje, codigoVuelo, estado:"liberado" }
//   GET  /api/slots?codigoVuelo=AR1234 -> { slotId, codigoVuelo, pista, gate, horarioProgramado, estado:"confirmado" }
// Autenticación: Authorization: ApiKey (se agrega al HttpClient al registrarlo). JSON camelCase, fechas ISO 8601 UTC.
// Errores de PortHub: { code, message, details }. Toda falla de red, timeout o respuesta inesperada se informa
// como AeropuertoNoDisponibleException (502); "slot ocupado" (409) como ConflictException.
public class HttpAeropuertoClient : IAeropuertoClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<HttpAeropuertoClient> _logger;

    public HttpAeropuertoClient(HttpClient http, ILogger<HttpAeropuertoClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> ConsultarDisponibilidadAsync(int pista, DateTime horario, CancellationToken ct = default)
    {
        var url = $"api/slots/disponibilidad?pista={pista}&horario={Uri.EscapeDataString(Iso(horario))}";
        using var resp = await EnviarAsync(() => _http.GetAsync(url, ct), "consultar disponibilidad");
        VerificarExito(resp, "consultar disponibilidad");
        return (await LeerAsync<DisponibilidadResp>(resp, "consultar disponibilidad", ct)).Disponible;
    }

    public async Task<SlotAeropuerto> SolicitarSlotAsync(SolicitudSlot s, CancellationToken ct = default)
    {
        var cuerpo = new SolicitudBody(s.CodigoVuelo, s.CodigoAerolinea, Iso(s.HorarioProgramado), s.Pista, s.Gate);
        using var contenido = JsonContent.Create(cuerpo, options: Json);
        await contenido.LoadIntoBufferAsync(); // envía Content-Length en vez de "chunked"

        using var resp = await EnviarAsync(() => _http.PostAsync("api/slots/solicitudes", contenido, ct), "solicitar slot");
        if (resp.StatusCode == HttpStatusCode.Conflict)
        {
            // Pista o gate ocupados en ese horario (ej: code SLOT_OCCUPIED): es un rechazo de negocio, no una caída.
            var (code, message) = await LeerErrorAsync(resp, ct);
            throw new ConflictException(code ?? "SLOT_OCCUPIED",
                message ?? "El aeropuerto informa que la pista o el gate no está disponible en el horario solicitado.");
        }
        VerificarExito(resp, "solicitar slot");
        return AModelo(await LeerAsync<SlotResp>(resp, "solicitar slot", ct));
    }

    public async Task<SlotAeropuerto?> ObtenerSlotPorVueloAsync(string codigoVuelo, CancellationToken ct = default)
    {
        var url = $"api/slots?codigoVuelo={Uri.EscapeDataString(codigoVuelo)}";
        using var resp = await EnviarAsync(() => _http.GetAsync(url, ct), "consultar slot");
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        VerificarExito(resp, "consultar slot");
        return AModelo(await LeerAsync<SlotResp>(resp, "consultar slot", ct));
    }

    public async Task LiberarSlotAsync(string codigoVuelo, CancellationToken ct = default)
    {
        using var contenido = JsonContent.Create(new CancelacionBody(codigoVuelo), options: Json);
        await contenido.LoadIntoBufferAsync();
        using var resp = await EnviarAsync(() => _http.PostAsync("api/slots/cancelaciones", contenido, ct), "liberar slot");
        if (resp.StatusCode == HttpStatusCode.NotFound) return; // el aeropuerto no tenía slot: ya está liberado
        VerificarExito(resp, "liberar slot");
    }

    // ---------- Auxiliares ----------

    // Equivalencia de estados PortHub -> AeroLink. Es el único lugar a tocar si el aeropuerto usa otros textos.
    // "reservado" = el aeropuerto reservó pero aún no confirmó (el vuelo NO puede confirmarse todavía).
    private static EstadoSlotAeropuerto EstadoDesde(string? estado) => estado?.Trim().ToLowerInvariant() switch
    {
        "reservado" or "pendiente" => EstadoSlotAeropuerto.Pendiente,
        "confirmado" or "otorgado" => EstadoSlotAeropuerto.Otorgado,
        "liberado" or "cancelado" => EstadoSlotAeropuerto.Liberado,
        "rechazado" or "denegado" => EstadoSlotAeropuerto.Rechazado,
        _ => throw new AeropuertoNoDisponibleException($"El aeropuerto informó un estado de slot desconocido: '{estado}'.")
    };

    private static SlotAeropuerto AModelo(SlotResp r) =>
        new(r.SlotId ?? string.Empty, r.CodigoVuelo ?? string.Empty, r.Pista, r.Gate, EstadoDesde(r.Estado));

    private static string Iso(DateTime fecha) =>
        fecha.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private async Task<HttpResponseMessage> EnviarAsync(Func<Task<HttpResponseMessage>> envio, string operacion)
    {
        try
        {
            return await envio();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            _logger.LogError(ex, "Falló la comunicación con el aeropuerto al {Operacion}", operacion);
            throw new AeropuertoNoDisponibleException($"No se pudo comunicar con el aeropuerto al {operacion}.");
        }
    }

    private void VerificarExito(HttpResponseMessage resp, string operacion)
    {
        if (resp.IsSuccessStatusCode) return;
        _logger.LogWarning("El aeropuerto respondió {Status} al {Operacion}", (int)resp.StatusCode, operacion);
        throw new AeropuertoNoDisponibleException(
            $"El aeropuerto rechazó la operación '{operacion}'.",
            new { statusAeropuerto = (int)resp.StatusCode });
    }

    private async Task<T> LeerAsync<T>(HttpResponseMessage resp, string operacion, CancellationToken ct) where T : class
    {
        try
        {
            return await resp.Content.ReadFromJsonAsync<T>(Json, ct)
                   ?? throw new AeropuertoNoDisponibleException($"El aeropuerto devolvió una respuesta vacía al {operacion}.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Respuesta inválida del aeropuerto al {Operacion}", operacion);
            throw new AeropuertoNoDisponibleException($"El aeropuerto devolvió una respuesta inválida al {operacion}.");
        }
    }

    // Lee el error unificado { code, message, details } de PortHub (tolerante: si no se puede, devuelve nulls).
    private static async Task<(string? Code, string? Message)> LeerErrorAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            string? Texto(string nombre) =>
                doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(nombre, out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetString() : null;
            return (Texto("code"), Texto("message"));
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private record SolicitudBody(string CodigoVuelo, string CodigoAerolinea, string HorarioProgramado, int Pista, string Gate);
    private record CancelacionBody(string CodigoVuelo);
    private record DisponibilidadResp(bool Disponible, int Pista, string? Horario);
    private record SlotResp(string? SlotId, string? CodigoVuelo, int Pista, string? Gate, string? Estado);
}
