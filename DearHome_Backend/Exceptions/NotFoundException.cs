namespace DearHome_Backend.Exceptions;

/// <summary>
/// The requested resource does not exist. Mapped to HTTP 404 by <c>ErrorHandlerMiddleware</c>.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
