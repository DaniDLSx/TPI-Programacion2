# AeroLink — API REST de la aerolínea

API REST (ASP.NET Core 8 · EF Core · SQLite) para administrar **vuelos, pasajeros, reservas y tickets**, integrada con la API del aeropuerto
para coordinar slots de despegue/aterrizaje. TP de *Programación 2 — Desarrollo de Servicios Backend* (UAP, 2026).

- Endpoints, parámetros y ejemplos → [`docs/ENDPOINTS.md`](docs/ENDPOINTS.md)
- Modelo de base de datos (ER + script SQL) → [`docs/MODELO_BD.md`](docs/MODELO_BD.md)
- Documentación interactiva → `/swagger` al ejecutar la API

## Cómo ejecutarla

Requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
cd src/AeroLink.Api
dotnet restore
dotnet tool install --global dotnet-ef          # una sola vez
dotnet ef migrations add Inicial                # genera la migración desde el modelo
dotnet run                                      # crea/actualiza aerolink.db y abre http://localhost:5000/swagger
```

> Si se ejecuta sin haber generado la migración, la API crea el esquema directamente desde el modelo (`EnsureCreated`).
> Si después se agrega la migración, borrar `aerolink.db` para que se cree desde cero con migraciones.

**Probar en Swagger:** botón **Authorize** → escribir `ApiKey dev-key-aerolink-123`.
También hay un recorrido completo listo para ejecutar en [`pruebas/AeroLink.http`](pruebas/AeroLink.http)
(extensión *REST Client* de VS Code, o Rider/Visual Studio).

## Configuración (`appsettings.json`)

| Clave | Para qué |
|---|---|
| `ConnectionStrings:AeroLinkDb` | Archivo SQLite (`Data Source=aerolink.db`) |
| `Seguridad:ApiKeys` | Lista de API Keys válidas. En producción usar variable de entorno `Seguridad__ApiKeys__0=...` (no subir claves reales al repositorio) |
| `Aeropuerto:Modo` | `Simulado` (cliente en memoria, para desarrollar sin la API real) o `Http` (API real) |
| `Aeropuerto:BaseUrl` / `ApiKey` | Dirección y clave de la API del aeropuerto (modo `Http`) |
| `Aeropuerto:CodigoAerolinea` | Código de aerolínea que AeroLink informa a PortHub (por defecto `ARG`) |

## Cómo funciona el flujo con el aeropuerto

```
1. POST /api/vuelos                       → vuelo Programado, sin slot
2. POST /api/vuelos/{id}/solicitar-slot   → body { pista, gate }: AeroLink consulta disponibilidad y pide el slot a PortHub (slot Solicitado)
3a. PortHub llama a POST /api/aeropuerto/callbacks/slots {OTORGADO} → AeroLink CONSULTA al aeropuerto y, si el slot figura otorgado → vuelo Confirmado
3b. o bien POST /api/vuelos/{id}/confirmar → AeroLink CONSULTA al aeropuerto y confirma solo si el slot está otorgado
4. POST /api/vuelos/{id}/cancelar         → avisa al aeropuerto (libera slot) + anula reservas y tickets
```

Criterios de aceptación del enunciado y dónde se cumplen:

| Criterio | Dónde |
|---|---|
| Un vuelo no se confirma sin slot validado por el aeropuerto | `VueloService.ConfirmarValidandoSlotAsync` (consulta al aeropuerto), usado tanto por `/confirmar` como por el callback. `PUT` no permite tocar el estado |
| Ticket con código único | `TicketService.GenerarCodigoUnicoAsync` + índice único `IX_Tickets_Codigo` |
| Cancelar un vuelo libera el slot y anula reservas | `VueloService.CancelarAsync` |
| Respuestas JSON con códigos HTTP adecuados | Controladores + `ExceptionHandlingMiddleware` |
| API Key `Authorization: ApiKey <clave>` en emisión, cancelaciones e integración | Atributo `[RequiereApiKey]` (`Security/ApiKeyAuthorization.cs`) |
| Validación y errores `{ code, message, details }` | Data Annotations en los DTOs + `ValidationErrorFactory` + middleware global |

## Estructura (capas vistas en la cátedra)

```
src/AeroLink.Api/
  Controllers/     Puerta HTTP: rutas y códigos de estado (delegan en los servicios)
  Services/        Lógica de negocio y reglas (lanzan excepciones de dominio)
  Repositories/    Interfaces de acceso a datos  +  Ef/ (implementación con EF Core)
  Data/            DbContext (modelo, índices, relaciones)
  Models/          Entidades y enums
  Dtos/            Objetos de entrada/salida con validaciones (Data Annotations)
  Integration/     Cliente de la API del aeropuerto (interfaz + HTTP + simulado)
  Security/        Filtro de API Key
  Middleware/      Manejo global de errores
  Exceptions/      Excepciones de dominio → códigos HTTP
  Extensions/      Registro de servicios (Program.cs queda corto)
```

Decisiones de diseño (para la defensa):

- **Inyección de dependencias y DIP**: los servicios dependen de interfaces (`IVueloRepository`, `IAeropuertoClient`, `IUnitOfWork`), no de EF ni de HTTP.
  Por eso el cliente del aeropuerto se puede cambiar (real/simulado) por configuración, y la lógica se puede probar sin base de datos.
- **Los servicios lanzan, el middleware traduce**: las reglas de negocio lanzan `ConflictException`/`NotFoundException`; un único middleware las convierte al formato de error.
- **El estado del vuelo no se edita libremente**: solo cambia por acciones (`confirmar`, `cancelar`, callback), así se garantiza la regla del slot.
- **El callback avisa, pero no confirma por sí solo**: antes de confirmar, AeroLink vuelve a consultar el slot a la API del aeropuerto. Un callback falso o adelantado recibe `409 SLOT_NOT_GRANTED` y el vuelo no cambia.
- **Cancelar es atómico respecto del aeropuerto**: primero se notifica al aeropuerto; si falla, se responde `502` y no se cambia nada.
- **Sin transacciones complejas** (como pide el enunciado): una operación = un `SaveChanges`.
- **No se borran datos**: "cancelar" cambia estados; se conserva la historia.
- **Zona horaria**: todo se guarda y devuelve en UTC.

## Contrato con el aeropuerto (PortHub)

`HttpAeropuertoClient` implementa el *Contrato de Integración API - PortHub y AeroLink*: `GET /api/slots/disponibilidad`,
`POST /api/slots/solicitudes`, `GET /api/slots?codigoVuelo=` y `POST /api/slots/cancelaciones`, con `Authorization: ApiKey <Aeropuerto:ApiKey>`.
AeroLink expone a PortHub `GET /api/tickets/{codigoTicket}/validacion` (control de embarque). Tabla completa y supuestos en
[`docs/ENDPOINTS.md`](docs/ENDPOINTS.md#contrato-de-integración-con-porthub-aeropuerto).

Para usar el aeropuerto real: `Aeropuerto:Modo = "Http"`, `Aeropuerto:BaseUrl` y `Aeropuerto:ApiKey`.
En modo `Simulado`, una solicitud queda `Pendiente` (`reservado`) y pasa a `Otorgado` (`confirmado`) cuando AeroLink la consulta
(en `/confirmar` o al recibir el callback); el callback se prueba a mano.

> Si ya habías ejecutado la versión anterior, **borrá `aerolink.db`**: el modelo ganó las columnas `Pista` y `Gate` en `Vuelos`.
