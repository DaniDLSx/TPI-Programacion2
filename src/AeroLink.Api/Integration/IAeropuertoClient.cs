namespace AeroLink.Api.Integration;

// Estado del slot visto desde AeroLink. Equivalencia con los estados de PortHub:
// "reservado" = Pendiente (reservado, falta que el aeropuerto lo confirme), "confirmado" = Otorgado,
// "liberado" = Liberado. (Ver HttpAeropuertoClient.EstadoDesde para ajustar la equivalencia.)
public enum EstadoSlotAeropuerto { Pendiente, Otorgado, Rechazado, Liberado }

//Datos de POST /api/slots/solicitudes (contrato PortHub).
public record SolicitudSlot(string CodigoVuelo, string CodigoAerolinea, DateTime HorarioProgramado, int Pista, string Gate);

public record SlotAeropuerto(string SlotId, string CodigoVuelo, int Pista, string? Gate, EstadoSlotAeropuerto Estado);


// Contrato de la API del aeropuerto (PortHub) desde el punto de vista de AeroLink. Los servicios
// dependen de esta interfaz (DIP): se puede usar el cliente HTTP real o el simulado.
// Cualquier falla de comunicación se informa con <c>AeropuertoNoDisponibleException</c>.
public interface IAeropuertoClient
{
    //GET /api/slots/disponibilidad: ¿la pista está libre en ese horario?
    Task<bool> ConsultarDisponibilidadAsync(int pista, DateTime horario, CancellationToken ct = default);

    //POST /api/slots/solicitudes. Si la pista/gate está ocupada lanza ConflictException (SLOT_OCCUPIED).
    Task<SlotAeropuerto> SolicitarSlotAsync(SolicitudSlot solicitud, CancellationToken ct = default);

    //GET /api/slots?codigoVuelo=... Devuelve null si el aeropuerto no tiene slot para ese vuelo.
    Task<SlotAeropuerto?> ObtenerSlotPorVueloAsync(string codigoVuelo, CancellationToken ct = default);

    //POST /api/slots/cancelaciones: libera pista y gate. Si ya no existe se considera liberado.
    Task LiberarSlotAsync(string codigoVuelo, CancellationToken ct = default);
}
