namespace AeroLink.Api.Dtos;

//Formato uniforme de error de toda la API.
public record ApiError(string Code, string Message, object? Details = null);
