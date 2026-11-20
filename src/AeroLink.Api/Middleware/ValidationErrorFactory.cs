using AeroLink.Api.Dtos;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AeroLink.Api.Middleware;

//Convierte los errores de validación de [ApiController] al formato { code, message, details }.
public static class ValidationErrorFactory
{
    public static ApiError Crear(ModelStateDictionary modelState)
    {
        // Clave vacía = cuerpo ausente; clave que empieza con '$' = JSON ilegible. En ambos casos el resto de los errores es consecuencia.
        var jsonIlegible = modelState.Keys.Any(k => k.Length == 0 || k.StartsWith('$'));
        var detalles = new Dictionary<string, string[]>();

        foreach (var (clave, entrada) in modelState)
        {
            if (entrada.Errors.Count == 0) continue;

            if (clave.Length == 0)
            {
                detalles[Campo(clave)] = entrada.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "El cuerpo de la solicitud es obligatorio." : e.ErrorMessage)
                    .ToArray();
            }
            else if (clave.StartsWith('$'))
            {
                detalles[Campo(clave)] = new[]
                {
                    clave == "$" ? "El cuerpo de la solicitud no es un JSON válido."
                                 : "El valor tiene un formato inválido o un tipo de dato incorrecto."
                };
            }
            else if (!jsonIlegible) // si el JSON no se pudo leer, los demás errores son consecuencia de eso
            {
                detalles[Campo(clave)] = entrada.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Valor inválido." : e.ErrorMessage)
                    .ToArray();
            }
        }
        return new ApiError("VALIDATION_ERROR", "Los datos enviados no son válidos.", detalles);
    }

    //"$.horarioSalida" -> "horarioSalida";  "Origen" -> "origen";  "" -> "(cuerpo)".
    private static string Campo(string clave)
    {
        var limpio = clave.TrimStart('$', '.');
        if (string.IsNullOrEmpty(limpio)) return "(cuerpo)";
        return string.Join('.', limpio.Split('.').Select(p => char.ToLowerInvariant(p[0]) + p[1..]));
    }
}
