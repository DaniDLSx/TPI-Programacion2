using System.Text.Json;
using AeroLink.Api.Dtos;
using AeroLink.Api.Exceptions;

namespace AeroLink.Api.Middleware;

// Red de seguridad global: traduce las AppException al formato { code, message, details }
// y cualquier error inesperado a un 500 genérico (sin filtrar detalles internos).
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            _logger.LogWarning("{Code} ({Status}): {Message}", ex.Code, ex.StatusCode, ex.Message);
            await Escribir(context, ex.StatusCode, new ApiError(ex.Code, ex.Message, ex.Details));
        }
        catch (BadHttpRequestException ex)
        {
            await Escribir(context, StatusCodes.Status400BadRequest, new ApiError("BAD_REQUEST", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en {Metodo} {Ruta}", context.Request.Method, context.Request.Path);
            await Escribir(context, StatusCodes.Status500InternalServerError,
                new ApiError("INTERNAL_ERROR", "Ocurrió un error inesperado. Intente nuevamente más tarde."));
        }
    }

    public static Task Escribir(HttpContext context, int status, ApiError error)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(JsonSerializer.Serialize(error, Json));
    }
}
