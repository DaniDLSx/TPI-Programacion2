using AeroLink.Api.Security;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AeroLink.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddAeroLinkSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AeroLink API",
                Version = "v1",
                Description = "API REST de la aerolínea: vuelos, pasajeros, reservas, tickets e integración con el aeropuerto."
            });

            // Comentarios XML (///) de controladores y DTOs.
            var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(SwaggerExtensions).Assembly.GetName().Name}.xml");
            if (File.Exists(xml)) o.IncludeXmlComments(xml);

            // Botón "Authorize": se escribe  ApiKey <clave>  y Swagger lo manda en Authorization.
            o.AddSecurityDefinition(RequiereApiKeyAttribute.Esquema, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Escribir: ApiKey <su-clave>   (ej: ApiKey dev-key-aerolink-123)"
            });
            o.OperationFilter<ApiKeyOperationFilter>();
        });
        return services;
    }

    //Marca con candado solo los endpoints que llevan [RequiereApiKey].
    public class ApiKeyOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var descriptor = context.ApiDescription.ActionDescriptor as ControllerActionDescriptor;
            var requiere = descriptor is not null &&
                (descriptor.MethodInfo.GetCustomAttributes(true).OfType<RequiereApiKeyAttribute>().Any() ||
                 descriptor.ControllerTypeInfo.GetCustomAttributes(true).OfType<RequiereApiKeyAttribute>().Any());
            if (!requiere) return;

            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new()
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = RequiereApiKeyAttribute.Esquema }
                    }] = Array.Empty<string>()
                }
            };
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "API Key ausente o inválida" });
        }
    }
}
