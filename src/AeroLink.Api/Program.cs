using AeroLink.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAeroLinkNegocio(builder.Configuration);   // controladores, servicios, cliente del aeropuerto
builder.Services.AddAeroLinkPersistencia(builder.Configuration); // EF Core + SQLite + repositorios
builder.Services.AddAeroLinkSwagger();                          // documentación OpenAPI

var app = builder.Build();

app.InicializarBaseDeDatos();
app.UseAeroLinkErrores(); // debe ir primero: atrapa lo que ocurra en el resto del pipeline

app.UseSwagger();
app.UseSwaggerUI(); // swagger

app.MapControllers();
app.Run();

public partial class Program { }
