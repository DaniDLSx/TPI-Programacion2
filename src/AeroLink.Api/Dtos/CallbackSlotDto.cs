using System.ComponentModel.DataAnnotations;

namespace AeroLink.Api.Dtos;

//Notificación (callback) que envía el aeropuerto cuando resuelve una solicitud de slot.
public class CallbackSlotDto : IValidatableObject
{
    //Id del slot (el que devolvió el aeropuerto). Se puede identificar el vuelo por este id o por codigoVuelo.
    /// <example>8f3d2e1a-4c2a-41b9-a012-123456789abc</example>
    [StringLength(60, ErrorMessage = "El campo {0} admite hasta {1} caracteres.")]
    public string? SlotId { get; set; }

    //Código del vuelo. Si vienen ambos, deben corresponder al mismo vuelo.
    // <example>AR1234</example>
    public string? CodigoVuelo { get; set; }

    //OTORGADO (o CONFIRMADO) / RECHAZADO; no distingue mayúsculas.
    // <example>OTORGADO</example>
    [Required(ErrorMessage = Msg.Requerido)]
    [RegularExpression(@"^(?i:otorgado|confirmado|rechazado)$",
        ErrorMessage = "El campo {0} debe ser OTORGADO, CONFIRMADO o RECHAZADO.")]
    public string? Estado { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(SlotId) && string.IsNullOrWhiteSpace(CodigoVuelo))
            yield return new ValidationResult("Debe indicar slotId o codigoVuelo.",
                new[] { nameof(SlotId), nameof(CodigoVuelo) });
    }
}
