using AeroLink.Api.Dtos;
using AeroLink.Api.Integration;
using AeroLink.Api.Middleware;
using AeroLink.Api.Security;
using AeroLink.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AeroLink.Api.Extensions;

public static class ServiceCollectionExtensions
{
    //Controladores, JSON, errores de validación uniformes, servicios y cliente del aeropuerto.
    public static IServiceCollection AddAeroLinkNegocio(this IServiceCollection services, IConfiguration config)
    {
        services.AddControllers(o =>
            {
                // Mensajes del binder (query string / ruta) en castellano, para que todos los errores sean consistentes.
                var msg = o.ModelBindingMessageProvider;
                msg.SetValueIsInvalidAccessor(valor => $"El valor '{valor}' no es válido.");
                msg.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"El valor '{valor}' no es válido para {campo}.");
                msg.SetValueMustBeANumberAccessor(campo => $"El campo {campo} debe ser un número.");
                // Parámetros de acción (?estado=..., ?vueloId=...): el binder usa las variantes "NonProperty".
                msg.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"El valor '{valor}' no es válido.");
                msg.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor enviado no es válido.");
                msg.SetNonPropertyValueMustBeANumberAccessor(() => "El valor debe ser un número.");
                msg.SetMissingRequestBodyRequiredValueAccessor(() => "El cuerpo de la solicitud es obligatorio.");
            })
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
            .ConfigureApiBehaviorOptions(o =>
            {
                // Los 4xx sin cuerpo (404, 405, 415) los completa UseStatusCodePages con el formato { code, message, details }.
                o.SuppressMapClientErrors = true;
                // Los errores de validación de [ApiController] salen con el mismo formato { code, message, details }.
                o.InvalidModelStateResponseFactory = ctx => new BadRequestObjectResult(ValidationErrorFactory.Crear(ctx.ModelState));
            });

        services.Configure<SeguridadOptions>(config.GetSection(SeguridadOptions.Seccion));
        services.Configure<AeropuertoOptions>(config.GetSection(AeropuertoOptions.Seccion));

        services.AddScoped<IVueloService, VueloService>();
        services.AddScoped<IPasajeroService, PasajeroService>();
        services.AddScoped<IReservaService, ReservaService>();
        services.AddScoped<ITicketService, TicketService>();

        var opciones = config.GetSection(AeropuertoOptions.Seccion).Get<AeropuertoOptions>() ?? new AeropuertoOptions();
        if (string.Equals(opciones.Modo, "Http", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAeropuertoClient, HttpAeropuertoClient>(http =>
            {
                http.BaseAddress = new Uri(opciones.BaseUrl.TrimEnd('/') + "/");
                http.Timeout = TimeSpan.FromSeconds(opciones.TimeoutSegundos);
                if (!string.IsNullOrWhiteSpace(opciones.ApiKey))
                    http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"ApiKey {opciones.ApiKey}");
            });
        }
        else
        {
            services.AddSingleton<IAeropuertoClient, AeropuertoSimuladoClient>();
        }
        return services;
    }

    //Middleware global de errores y respuestas JSON para rutas inexistentes / métodos no permitidos.
    public static WebApplication UseAeroLinkErrores(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseStatusCodePages(ctx =>
        {
            var status = ctx.HttpContext.Response.StatusCode;
            var error = status switch
            {
                404 => new ApiError("NOT_FOUND", "El recurso solicitado no existe."),
                405 => new ApiError("METHOD_NOT_ALLOWED", "El método HTTP no está permitido para este recurso."),
                415 => new ApiError("UNSUPPORTED_MEDIA_TYPE", "El cuerpo debe enviarse como application/json."),
                _ => new ApiError("HTTP_" + status, "La solicitud no pudo procesarse.")
            };
            return ExceptionHandlingMiddleware.Escribir(ctx.HttpContext, status, error);
        });
        return app;
    }
}
