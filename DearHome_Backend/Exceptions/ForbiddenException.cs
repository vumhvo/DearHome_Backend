namespace DearHome_Backend.Exceptions;

/// <summary>
/// The caller is authenticated but not allowed to perform the operation. Mapped to HTTP 403 by <c>ErrorHandlerMiddleware</c>.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
