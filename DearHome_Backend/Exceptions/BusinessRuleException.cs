namespace DearHome_Backend.Exceptions;

/// <summary>
/// A validation or business rule failed (invalid credentials, invalid verification code, duplicate data, ...).
/// The message is meant for the caller. Mapped to HTTP 400 by <c>ErrorHandlerMiddleware</c>.
/// </summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }

    public BusinessRuleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
