namespace AeroLink.Api.Integration;

//Sección "Aeropuerto" de appsettings.json.
public class AeropuertoOptions
{
    public const string Seccion = "Aeropuerto";

    // <summary>"Simulado" (cliente en memoria, para desarrollo) o "Http" (API real de PortHub).</summary>
    public string Modo { get; set; } = "Simulado";
    public string BaseUrl { get; set; } = "http://localhost:6000/";
    // <summary>Clave que AeroLink envía a PortHub (Authorization: ApiKey ...).</summary>
    public string ApiKey { get; set; } = string.Empty;
    // <summary>Código de la aerolínea que AeroLink informa a PortHub en cada solicitud de slot.</summary>
    public string CodigoAerolinea { get; set; } = "ARG";
    public int TimeoutSegundos { get; set; } = 10;
}
