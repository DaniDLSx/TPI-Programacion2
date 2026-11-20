namespace AeroLink.Api.Models;

/// <summary>Estado del vuelo. Solo pasa a Confirmado con un slot otorgado por el aeropuerto.</summary>
public enum EstadoVuelo { Programado, Confirmado, Cancelado }

/// <summary>Estado del slot de despegue/aterrizaje gestionado con el aeropuerto.</summary>
public enum EstadoSlot { SinSlot, Solicitado, Otorgado, Rechazado, Liberado }

public enum EstadoReserva { Pendiente, Confirmada, Anulada }

public enum EstadoTicket { Emitido, Anulado }
