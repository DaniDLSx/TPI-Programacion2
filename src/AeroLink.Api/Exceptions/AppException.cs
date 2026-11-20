namespace AeroLink.Api.Exceptions;

// Excepción base de la aplicación. La capa de servicios lanza estas excepciones y el
// middleware global las traduce al formato uniforme { code, message, details }.
public class AppException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    public object? Details { get; }

    public AppException(int statusCode, string code, string message, object? details = null) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }
}

//404: el recurso pedido no existe.
public class NotFoundException : AppException
{
    public NotFoundException(string code, string message, object? details = null)
        : base(StatusCodes.Status404NotFound, code, message, details) { }
}

//409: la operación choca con el estado actual (regla de negocio).
public class ConflictException : AppException
{
    public ConflictException(string code, string message, object? details = null)
        : base(StatusCodes.Status409Conflict, code, message, details) { }
}

//502: falló la comunicación con la API del aeropuerto.
public class AeropuertoNoDisponibleException : AppException
{
    public AeropuertoNoDisponibleException(string message, object? details = null)
        : base(StatusCodes.Status502BadGateway, "AIRPORT_UNAVAILABLE", message, details) { }
}
