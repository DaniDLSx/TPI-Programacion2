# AeroLink — Documentación de endpoints

Base URL (desarrollo): `http://localhost:5000` · Swagger interactivo: `http://localhost:5000/swagger`

## Convenciones

- Todas las respuestas son **JSON**. Fechas en **ISO-8601 UTC** (`2026-12-01T10:00:00Z`; si se envían sin zona se asumen UTC).
- Los estados se envían y reciben como texto (`"Programado"`, `"Confirmada"`…).
- 🔒 = requiere **API Key** en el encabezado: `Authorization: ApiKey <clave>`.
  Sin clave o con clave inválida → `401`.
- **Formato único de error** (todos los casos: validación, negocio, 404, 401, 502, 500):

```json
{
  "code": "VALIDATION_ERROR",
  "message": "Los datos enviados no son válidos.",
  "details": { "origen": ["El campo Origen debe ser un código IATA de 3 letras (ej: EZE)."] }
}
```

`code` es estable (para que lo use un programa); `message` es legible; `details` es opcional (`null` si no aplica).

### Códigos HTTP usados

| Código | Cuándo |
|---|---|
| 200 | Consulta o acción exitosa |
| 201 | Recurso creado (incluye encabezado `Location`) |
| 400 | Datos inválidos (`VALIDATION_ERROR`) |
| 401 | API Key ausente o inválida |
| 404 | El recurso no existe |
| 405 / 415 | Método no permitido / el cuerpo no es JSON |
| 409 | La operación choca con el estado actual (regla de negocio) |
| 502 | El aeropuerto no responde o rechazó la operación (`AIRPORT_UNAVAILABLE`) |
| 500 | Error inesperado (`INTERNAL_ERROR`) |

---

## Vuelos — `/api/vuelos`

### `GET /api/vuelos`
Lista vuelos ordenados por horario de salida.
Query opcional: `estado` (`Programado|Confirmado|Cancelado`), `origen`, `destino` (código IATA).

```json
[
  {
    "id": 1, "codigoVuelo": "AL1234", "origen": "EZE", "destino": "AEP",
    "horarioSalida": "2026-12-01T10:00:00Z", "horarioLlegada": "2026-12-01T12:30:00Z",
    "aeronave": "LV-ABC", "estado": "Programado", "estadoSlot": "SinSlot", "slotId": null,
    "pista": null, "gate": null
  }
]
```

### `GET /api/vuelos/{id}`
Devuelve un vuelo (mismo formato). `404 FLIGHT_NOT_FOUND` si no existe.

### `POST /api/vuelos`
Registra un vuelo. Queda `Programado` y `SinSlot`.

| Campo | Tipo | Reglas |
|---|---|---|
| `codigoVuelo` | string | obligatorio, 2 letras + 3 o 4 dígitos (`AL1234`), único |
| `origen`, `destino` | string | obligatorios, código IATA de 3 letras, distintos entre sí |
| `horarioSalida`, `horarioLlegada` | fecha-hora | obligatorios, llegada posterior a salida |
| `aeronave` | string | obligatorio, 2–20 caracteres (matrícula) |

```json
{ "codigoVuelo": "AL1234", "origen": "EZE", "destino": "AEP",
  "horarioSalida": "2026-12-01T10:00:00Z", "horarioLlegada": "2026-12-01T12:30:00Z", "aeronave": "LV-ABC" }
```
Respuesta `201` con el vuelo creado. Errores: `400 VALIDATION_ERROR`, `409 FLIGHT_CODE_DUPLICATED`.

### `PUT /api/vuelos/{id}`
Modifica `origen`, `destino`, `horarioSalida`, `horarioLlegada`, `aeronave` (mismas reglas; **no** lleva `codigoVuelo` ni `estado`:
el estado solo cambia por las acciones de abajo). Respuesta `200` con el vuelo.
Errores: `404`, `409 FLIGHT_CANCELLED`, `409 FLIGHT_SLOT_LOCKED` (con un slot solicitado u otorgado no se puede cambiar
origen/destino/horarios, porque el slot corresponde a ese itinerario; sí se puede cambiar la aeronave).

### `POST /api/vuelos/{id}/solicitar-slot` 🔒
Pide al aeropuerto (PortHub) la reserva de un slot para el vuelo. Primero **consulta la disponibilidad** de la pista en el horario de salida
(`GET /api/slots/disponibilidad`) y, si está libre, hace la **solicitud formal** (`POST /api/slots/solicitudes`).
El vuelo sigue `Programado`; el slot queda `Solicitado` (el aeropuerto lo tiene "reservado", falta su confirmación).

| Campo | Reglas |
|---|---|
| `pista` | obligatorio, entero 1–99 |
| `gate` | obligatorio, una letra + 1 o 2 dígitos (`G04`) |

```json
{ "pista": 1, "gate": "G04" }
```
```json
{ "id": 1, "codigoVuelo": "AL1234", "origen": "EZE", "destino": "AEP",
  "horarioSalida": "2026-12-01T10:00:00Z", "horarioLlegada": "2026-12-01T12:30:00Z",
  "aeronave": "LV-ABC", "estado": "Programado", "estadoSlot": "Solicitado",
  "slotId": "8f3d2e1a-4c2a-41b9-a012-123456789abc", "pista": 1, "gate": "G04" }
```
Errores: `400 VALIDATION_ERROR`, `409 FLIGHT_CANCELLED | FLIGHT_ALREADY_CONFIRMED | SLOT_ALREADY_REQUESTED`,
`409 SLOT_NOT_AVAILABLE` (la pista no está disponible en ese horario, según la consulta previa),
`409 SLOT_OCCUPIED` (el aeropuerto rechazó la solicitud por pista o gate ocupados; trae el mensaje del aeropuerto), `502 AIRPORT_UNAVAILABLE`.
Si el aeropuerto rechazó un slot anterior (`Rechazado`), se puede volver a solicitar.

### `POST /api/vuelos/{id}/confirmar` 🔒
**Consulta al aeropuerto** (`GET /api/slots?codigoVuelo=...`) el estado del slot y confirma el vuelo **solo si está otorgado** (`confirmado` en PortHub). Es idempotente (si ya está confirmado devuelve `200`).

Respuesta `200`: el vuelo con `"estado": "Confirmado", "estadoSlot": "Otorgado"`.
Errores:
- `409 SLOT_NOT_REQUESTED` — nunca se solicitó slot.
- `409 SLOT_NOT_GRANTED` — el aeropuerto aún no lo otorgó: `"details": { "slotId": "8f3d2e1a-4c2a-41b9-a012-123456789abc", "estadoAeropuerto": "Pendiente" }` (PortHub lo tiene `reservado`, aún no `confirmado`).
- `409 SLOT_REJECTED` — el slot fue rechazado; solicitar uno nuevo.
- `409 FLIGHT_CANCELLED`, `502 AIRPORT_UNAVAILABLE`.

### `POST /api/vuelos/{id}/cancelar` 🔒
Cancela el vuelo. Orden de operaciones: (1) **notifica al aeropuerto** (`POST /api/slots/cancelaciones`) para liberar pista y gate (si tenía un slot vigente), (2) **anula todas las reservas** no anuladas y sus **tickets**, (3) marca el vuelo `Cancelado` y el slot `Liberado`.
Si el aeropuerto no responde, devuelve `502` y **no se modifica nada** (se puede reintentar).

```json
{
  "vuelo": { "id": 1, "codigoVuelo": "AL1234", "estado": "Cancelado", "estadoSlot": "Liberado", "slotId": "8f3d2e1a-4c2a-41b9-a012-123456789abc",
             "origen": "EZE", "destino": "AEP", "horarioSalida": "2026-12-01T10:00:00Z",
             "horarioLlegada": "2026-12-01T12:30:00Z", "aeronave": "LV-ABC" },
  "reservasAnuladas": 2,
  "ticketsAnulados": 1,
  "slotLiberado": true
}
```
Errores: `404`, `409 FLIGHT_ALREADY_CANCELLED`, `502 AIRPORT_UNAVAILABLE`.

### `GET /api/vuelos/consolidado` — *desafío adicional*
Combina **ambas APIs**: por cada vuelo, su estado de slot local, el estado de slot **consultado en vivo al aeropuerto**, y sus reservas.
Query opcional: `estado`. Las consultas al aeropuerto se hacen en paralelo (máx. 5 simultáneas). Si el aeropuerto falla para un vuelo,
la consulta **no se cae**: ese vuelo informa `estado: "NoDisponible"`.

```json
[
  {
    "vuelo": { "id": 1, "codigoVuelo": "AL1234", "estado": "Confirmado", "estadoSlot": "Otorgado", "slotId": "8f3d2e1a-4c2a-41b9-a012-123456789abc", "pista": 1, "gate": "G04",
               "origen": "EZE", "destino": "AEP", "horarioSalida": "2026-12-01T10:00:00Z",
               "horarioLlegada": "2026-12-01T12:30:00Z", "aeronave": "LV-ABC" },
    "slotAeropuerto": { "slotId": "8f3d2e1a-4c2a-41b9-a012-123456789abc", "pista": 1, "gate": "G04", "estado": "Otorgado", "consultado": true, "error": null },
    "slotSincronizado": true,
    "reservas": {
      "total": 2, "activas": 2,
      "items": [
        { "id": 1, "pasajero": "Ana Gómez", "asiento": "12A", "estado": "Confirmada", "ticketCodigo": "AL-7K2M9XQ4TB" },
        { "id": 2, "pasajero": "Beto Ruiz", "asiento": "14C", "estado": "Pendiente",  "ticketCodigo": null }
      ]
    }
  }
]
```
- `slotAeropuerto` es `null` si el vuelo nunca pidió slot.
- `slotSincronizado` indica si el estado local coincide con el del aeropuerto (`null` si no se pudo consultar).
- `estado` del slot en el aeropuerto: `Pendiente | Otorgado | Rechazado | Liberado | Inexistente | NoDisponible`.

---

## Integración con el aeropuerto — `/api/aeropuerto`

### `POST /api/aeropuerto/callbacks/slots` 🔒
**Callback** que invoca el aeropuerto cuando resuelve una solicitud de slot.

| Campo | Reglas |
|---|---|
| `slotId` | el que devolvió `solicitar-slot` (obligatorio si no se envía `codigoVuelo`) |
| `codigoVuelo` | identifica al vuelo (obligatorio si no se envía `slotId`); si vienen ambos, deben coincidir |
| `estado` | obligatorio: `OTORGADO` (o `CONFIRMADO`) / `RECHAZADO` (no distingue mayúsculas) |

```json
{ "codigoVuelo": "AL1234", "estado": "OTORGADO" }
```
- `OTORGADO` → AeroLink **consulta al aeropuerto** el estado real del slot y, solo si figura otorgado, marca el slot `Otorgado` y el vuelo **`Confirmado`** (misma validación que `/confirmar`: un callback falso o adelantado no confirma nada).
- `RECHAZADO` → slot `Rechazado`; el vuelo queda (o vuelve a) `Programado`.

Respuesta `200` con el vuelo actualizado. Errores: `404 SLOT_NOT_FOUND`, `409 SLOT_FLIGHT_MISMATCH`, `409 FLIGHT_CANCELLED`,
`409 SLOT_NOT_GRANTED` (el aeropuerto no informa el slot como otorgado), `502 AIRPORT_UNAVAILABLE` (no se pudo validar: el aeropuerto puede reintentar), `400`.

---

## Pasajeros — `/api/pasajeros`

### `GET /api/pasajeros` · `GET /api/pasajeros/{id}` · `GET /api/pasajeros/{id}/reservas`
Lista, obtiene un pasajero, o lista sus reservas. `404 PASSENGER_NOT_FOUND`.

### `POST /api/pasajeros`
| Campo | Reglas |
|---|---|
| `nombre`, `apellido` | obligatorios, hasta 60 caracteres |
| `documento` | obligatorio, alfanumérico de 6 a 15, **único** |
| `email` | obligatorio, formato de correo válido |

```json
{ "nombre": "Ana", "apellido": "Gómez", "documento": "30123456", "email": "ana@mail.com" }
```
Respuesta `201`:
```json
{ "id": 1, "nombre": "Ana", "apellido": "Gómez", "documento": "30123456", "email": "ana@mail.com" }
```
Errores: `400 VALIDATION_ERROR`, `409 PASSENGER_DOCUMENT_DUPLICATED`.

---

## Reservas — `/api/reservas`

### `GET /api/reservas`
Query opcional: `vueloId`, `pasajeroId`, `estado` (`Pendiente|Confirmada|Anulada`).

### `GET /api/reservas/{id}`
```json
{ "id": 1, "pasajeroId": 1, "pasajero": "Ana Gómez", "vueloId": 1, "codigoVuelo": "AL1234",
  "asiento": "12A", "estado": "Confirmada", "fechaEmision": "2026-11-14T07:01:16Z", "ticketCodigo": "AL-7K2M9XQ4TB" }
```

### `POST /api/reservas`
Vincula un pasajero con un vuelo. Queda `Pendiente`, con la fecha de emisión actual.

| Campo | Reglas |
|---|---|
| `pasajeroId`, `vueloId` | obligatorios, deben existir |
| `asiento` | obligatorio, fila 1–59 + letra A–F (`12A`) |

```json
{ "pasajeroId": 1, "vueloId": 1, "asiento": "12A" }
```
Respuesta `201`. Errores: `400`, `404 FLIGHT_NOT_FOUND | PASSENGER_NOT_FOUND`, `409 FLIGHT_CANCELLED`,
`409 SEAT_TAKEN` (asiento ya reservado en ese vuelo), `409 RESERVATION_DUPLICATED` (el pasajero ya tiene una reserva activa en ese vuelo).

### `POST /api/reservas/{id}/confirmar`
`Pendiente` → `Confirmada` (idempotente). Errores: `409 RESERVATION_CANCELLED | FLIGHT_CANCELLED`.

### `POST /api/reservas/{id}/cancelar` 🔒
Anula la reserva (libera el asiento) y su ticket, si lo tiene. Errores: `404`, `409 RESERVATION_ALREADY_CANCELLED`.

---

## Tickets — `/api/tickets`

### `POST /api/tickets` 🔒
Emite el ticket electrónico de una reserva **confirmada**. Cuerpo: `{ "reservaId": 1 }`.
El código es **único**: `AL-` + 10 caracteres aleatorios criptográficamente seguros, verificados contra la base y protegidos por índice único.

Respuesta `201`:
```json
{ "id": 1, "codigo": "AL-7K2M9XQ4TB", "reservaId": 1, "codigoVuelo": "AL1234",
  "pasajero": "Ana Gómez", "asiento": "12A", "estado": "Emitido", "fechaEmision": "2026-11-14T07:05:00Z" }
```
Errores: `404 RESERVATION_NOT_FOUND`, `409 RESERVATION_NOT_CONFIRMED`, `409 TICKET_ALREADY_ISSUED`.

### `GET /api/tickets/{codigoTicket}/validacion` 🔒 — *lo consume PortHub en el embarque*
Servicio que exige el contrato de integración: verifica si el ticket es **válido y está vigente** antes de entrar a la zona restringida.
Siempre responde `200`; si no es válido, `esValido` es `false` y `motivo` explica por qué (ticket inexistente, anulado, reserva no confirmada, vuelo cancelado o ya finalizado).

```json
{ "esValido": true, "codigoTicket": "AL-7K2M9XQ4TB", "codigoVuelo": "AL1234", "pasajero": "Ana Gómez", "motivo": null }
```
```json
{ "esValido": false, "codigoTicket": "AL-7K2M9XQ4TB", "codigoVuelo": "AL1234", "pasajero": "Ana Gómez", "motivo": "El ticket fue anulado." }
```

### `GET /api/tickets` · `GET /api/tickets/{codigo}`
Lista (filtro opcional `reservaId`) o consulta por código (no distingue mayúsculas). `404 TICKET_NOT_FOUND`.

---

## Catálogo de códigos de error

| `code` | HTTP | Significado |
|---|---|---|
| `VALIDATION_ERROR` | 400 | Datos inválidos; `details` indica campo por campo |
| `API_KEY_MISSING` / `API_KEY_INVALID` | 401 | Falta la API Key o es incorrecta |
| `NOT_FOUND` / `METHOD_NOT_ALLOWED` / `UNSUPPORTED_MEDIA_TYPE` | 404 / 405 / 415 | Ruta inexistente, método no permitido, cuerpo no JSON |
| `FLIGHT_NOT_FOUND`, `PASSENGER_NOT_FOUND`, `RESERVATION_NOT_FOUND`, `TICKET_NOT_FOUND`, `SLOT_NOT_FOUND` | 404 | El recurso no existe |
| `FLIGHT_CODE_DUPLICATED`, `PASSENGER_DOCUMENT_DUPLICATED` | 409 | Dato único repetido |
| `FLIGHT_CANCELLED`, `FLIGHT_ALREADY_CANCELLED`, `FLIGHT_ALREADY_CONFIRMED`, `FLIGHT_SLOT_LOCKED` | 409 | Estado del vuelo no permite la operación |
| `SLOT_NOT_REQUESTED`, `SLOT_ALREADY_REQUESTED`, `SLOT_NOT_AVAILABLE`, `SLOT_OCCUPIED`, `SLOT_NOT_GRANTED`, `SLOT_REJECTED`, `SLOT_FLIGHT_MISMATCH` | 409 | Estado del slot no permite la operación |
| `SEAT_TAKEN`, `RESERVATION_DUPLICATED`, `RESERVATION_CANCELLED`, `RESERVATION_ALREADY_CANCELLED`, `RESERVATION_NOT_CONFIRMED` | 409 | Reglas de reservas |
| `TICKET_ALREADY_ISSUED` | 409 | La reserva ya tiene ticket |
| `AIRPORT_UNAVAILABLE` | 502 | Falla la comunicación con el aeropuerto |
| `INTERNAL_ERROR` | 500 | Error inesperado (el detalle queda solo en el log) |


---

## Contrato de integración con PortHub (aeropuerto)

AeroLink consume estos servicios de PortHub (según el documento *Contrato de Integración API - PortHub y AeroLink*; ApiKey en cada llamada, JSON camelCase, fechas ISO 8601 UTC):

| AeroLink hace… | Llamada a PortHub | Estado en PortHub → en AeroLink |
|---|---|---|
| Consultar disponibilidad (antes de solicitar) | `GET /api/slots/disponibilidad?pista=1&horario=2026-12-01T10:00:00Z` | — |
| Solicitar el slot | `POST /api/slots/solicitudes` `{ codigoVuelo, codigoAerolinea, horarioProgramado, pista, gate }` | `reservado` → slot `Solicitado` |
| Validar antes de confirmar el vuelo | `GET /api/slots?codigoVuelo=AL1234` | `confirmado` → `Otorgado` (el vuelo puede confirmarse) |
| Liberar al cancelar el vuelo | `POST /api/slots/cancelaciones` `{ codigoVuelo }` | `liberado` → `Liberado` |

Y expone a PortHub: `GET /api/tickets/{codigoTicket}/validacion` (ver Tickets) y el callback `POST /api/aeropuerto/callbacks/slots`.

> Supuestos (el contrato no los define): `reservado` = el aeropuerto reservó pero aún no confirmó; `confirmado` = slot otorgado.
> El `horarioProgramado` enviado es el horario de **salida** del vuelo, y `codigoAerolinea` sale de `Aeropuerto:CodigoAerolinea` (por defecto `ARG`).
> El callback no figura en el contrato: se mantiene porque lo pide el enunciado; sirve como aviso y la confirmación igual se valida con `GET /api/slots`.
