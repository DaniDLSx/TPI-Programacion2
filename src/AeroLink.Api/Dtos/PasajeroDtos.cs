using System.ComponentModel.DataAnnotations;
using AeroLink.Api.Models;

namespace AeroLink.Api.Dtos;

public class PasajeroCrearDto
{
    // <example>Ana</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [StringLength(60, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener hasta {1} caracteres.")]
    public string? Nombre { get; set; }

    // <example>Gómez</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [StringLength(60, MinimumLength = 1, ErrorMessage = "El campo {0} debe tener hasta {1} caracteres.")]
    public string? Apellido { get; set; }

    // <example>30123456</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [RegularExpression(@"^[A-Za-z0-9]{6,15}$",
        ErrorMessage = "El campo {0} debe ser alfanumérico, de 6 a 15 caracteres.")]
    public string? Documento { get; set; }

    // <example>ana@mail.com</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [EmailAddress(ErrorMessage = "El campo {0} no tiene un formato de correo válido.")]
    [StringLength(120, ErrorMessage = "El campo {0} admite hasta {1} caracteres.")]
    public string? Email { get; set; }
}

public class PasajeroDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public static PasajeroDto Desde(Pasajero p) => new()
    {
        Id = p.Id, Nombre = p.Nombre, Apellido = p.Apellido, Documento = p.Documento, Email = p.Email
    };
}
