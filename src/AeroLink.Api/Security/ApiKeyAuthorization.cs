using System.Security.Cryptography;
using System.Text;
using AeroLink.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace AeroLink.Api.Security;

//Sección "Seguridad" de la configuración. En producción: variable Seguridad__ApiKeys__0.
public class SeguridadOptions
{
    public const string Seccion = "Seguridad";
    public string[] ApiKeys { get; set; } = Array.Empty<string>();
}

/// Exige el encabezado  Authorization: ApiKey &lt;clave&gt;  en la acción o controlador marcado.
/// Responde 401 con el formato uniforme de error si falta o es inválida.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequiereApiKeyAttribute : TypeFilterAttribute
{
    public const string Esquema = "ApiKey";
    public RequiereApiKeyAttribute() : base(typeof(ApiKeyAuthorizationFilter)) { }
}

public class ApiKeyAuthorizationFilter : IAuthorizationFilter
{
    private readonly byte[][] _hashesValidos;
    private readonly ILogger<ApiKeyAuthorizationFilter> _logger;

    public ApiKeyAuthorizationFilter(IOptions<SeguridadOptions> options, ILogger<ApiKeyAuthorizationFilter> logger)
    {
        _logger = logger;
        _hashesValidos = options.Value.ApiKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(Hash)
            .ToArray();
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var header = context.HttpContext.Request.Headers.Authorization.ToString();
        var prefijo = RequiereApiKeyAttribute.Esquema + " ";

        if (string.IsNullOrWhiteSpace(header))
        {
            Rechazar(context, "API_KEY_MISSING", "Falta el encabezado 'Authorization: ApiKey <clave>'.");
            return;
        }
        if (!header.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
        {
            Rechazar(context, "API_KEY_INVALID", "El encabezado Authorization debe tener el formato 'ApiKey <clave>'.");
            return;
        }

        var clave = header[prefijo.Length..].Trim();
        var hash = Hash(clave);
        // Comparación en tiempo constante para no filtrar información por diferencias de tiempo.
        var valida = _hashesValidos.Any(h => CryptographicOperations.FixedTimeEquals(h, hash));
        if (!valida)
            Rechazar(context, "API_KEY_INVALID", "La API Key es inválida.");
    }

    private void Rechazar(AuthorizationFilterContext context, string code, string message)
    {
        _logger.LogWarning("Acceso rechazado a {Ruta}: {Codigo}", context.HttpContext.Request.Path, code);
        context.HttpContext.Response.Headers.WWWAuthenticate = RequiereApiKeyAttribute.Esquema;
        context.Result = new ObjectResult(new ApiError(code, message)) { StatusCode = StatusCodes.Status401Unauthorized };
    }

    private static byte[] Hash(string valor) => SHA256.HashData(Encoding.UTF8.GetBytes(valor));
}
