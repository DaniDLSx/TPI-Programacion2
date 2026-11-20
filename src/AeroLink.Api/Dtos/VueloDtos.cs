using System.ComponentModel.DataAnnotations;
using AeroLink.Api.Models;

namespace AeroLink.Api.Dtos;

//Datos modificables de un vuelo (el código y el estado no se editan por PUT).
public class VueloActualizarDto : IValidatableObject
{
    // <example>EZE</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = Msg.Iata)]
    public string? Origen { get; set; }

    // <example>AEP</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [RegularExpression(@"^[A-Za-z]{3}$", ErrorMessage = Msg.Iata)]
    public string? Destino { get; set; }

    // <example>2026-12-01T10:00:00Z</example>
    [Required(ErrorMessage = Msg.Requerido)]
    public DateTime? HorarioSalida { get; set; }

    // <example>2026-12-01T12:30:00Z</example>
    [Required(ErrorMessage = Msg.Requerido)]
    public DateTime? HorarioLlegada { get; set; }

    // <example>LV-ABC</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "El campo {0} debe tener entre {2} y {1} caracteres.")]
    public string? Aeronave { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (HorarioSalida.HasValue && HorarioLlegada.HasValue && HorarioLlegada <= HorarioSalida)
            yield return new ValidationResult("El horario de llegada debe ser posterior al de salida.",
                new[] { nameof(HorarioLlegada) });

        if (!string.IsNullOrWhiteSpace(Origen) && string.Equals(Origen, Destino, StringComparison.OrdinalIgnoreCase))
            yield return new ValidationResult("El origen y el destino no pueden ser iguales.",
                new[] { nameof(Destino) });
    }
}

public class VueloCrearDto : VueloActualizarDto
{
    // <example>AL1234</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [RegularExpression(@"^[A-Za-z]{2}\d{3,4}$",
        ErrorMessage = "El campo {0} debe tener 2 letras y 3 o 4 dígitos (ej: AL1234).")]
    public string? CodigoVuelo { get; set; }
}

public class VueloDto
{
    public int Id { get; set; }
    public string CodigoVuelo { get; set; } = string.Empty;
    public string Origen { get; set; } = string.Empty;
    public string Destino { get; set; } = string.Empty;
    public DateTime HorarioSalida { get; set; }
    public DateTime HorarioLlegada { get; set; }
    public string Aeronave { get; set; } = string.Empty;
    public EstadoVuelo Estado { get; set; }
    public EstadoSlot EstadoSlot { get; set; }
    public string? SlotId { get; set; }
    public int? Pista { get; set; }
    public string? Gate { get; set; }

    public static VueloDto Desde(Vuelo v) => new()
    {
        Id = v.Id,
        CodigoVuelo = v.CodigoVuelo,
        Origen = v.Origen,
        Destino = v.Destino,
        HorarioSalida = v.HorarioSalida,
        HorarioLlegada = v.HorarioLlegada,
        Aeronave = v.Aeronave,
        Estado = v.Estado,
        EstadoSlot = v.EstadoSlot,
        SlotId = v.SlotId,
        Pista = v.Pista,
        Gate = v.Gate
    };
}

//Cuerpo de POST /api/vuelos/{id}/solicitar-slot: pista y gate que se piden al aeropuerto.
public class SolicitarSlotDto
{
    // <example>1</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [Range(1, 10, ErrorMessage = "El campo {0} debe estar entre {1} y {2}.")]
    public int? Pista { get; set; }

    // <example>G04</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [RegularExpression(@"^[A-Za-z]\d{1,2}$", ErrorMessage = "El campo {0} debe ser una letra y 1 o 2 dígitos (ej: G04).")]
    public string? Gate { get; set; }
}

//Resultado de cancelar un vuelo: qué se anuló en cascada.
public class CancelacionVueloDto
{
    public VueloDto Vuelo { get; set; } = new();
    public int ReservasAnuladas { get; set; }
    public int TicketsAnulados { get; set; }
    public bool SlotLiberado { get; set; }
}


