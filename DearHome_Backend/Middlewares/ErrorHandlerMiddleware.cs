using System.Net;
using System.Text.Json;
using DearHome_Backend.Exceptions;

namespace DearHome_Backend.Middlewares;

/// <summary>
/// Converts unhandled exceptions into a JSON error response: { "message": ..., "statusCode": ... }.
/// The frontend reads <c>error.response.data.message</c>, so that shape must stay stable.
/// </summary>
public class ErrorHandlerMiddleware
{
    private const string GenericErrorMessage = "An unexpected error occurred. Please try again later.";

    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; there is nobody to send a response to.
            _logger.LogDebug("Request {Method} {Path} was cancelled by the client.", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Unhandled exception after the response started for {Method} {Path}.", context.Request.Method, context.Request.Path);
                throw;
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = GetStatusCode(context, exception);
        var isServerError = statusCode >= StatusCodes.Status500InternalServerError;

        if (isServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}.", context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Request {Method} {Path} failed with {StatusCode}: {Message}", context.Request.Method, context.Request.Path, statusCode, exception.Message);
        }

        // Client errors carry a message meant for the caller. Server errors only expose details in Development.
        var message = isServerError && !_environment.IsDevelopment() ? GenericErrorMessage : exception.Message;

        object errorResponse = _environment.IsDevelopment()
            ? new { message, statusCode, detail = exception.ToString() }
            : new { message, statusCode };

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
    }

    // InvalidOperationException is deliberately not mapped: it is what configuration and EF Core internals throw,
    // so it falls through to 500 and its message is hidden outside Development. Business/validation failures
    // must throw BusinessRuleException (400) or NotFoundException (404) instead.
    private static int GetStatusCode(HttpContext context, Exception exception) => exception switch
    {
        NotFoundException or KeyNotFoundException => (int)HttpStatusCode.NotFound,
        BusinessRuleException => (int)HttpStatusCode.BadRequest,
        BadHttpRequestException badRequest => badRequest.StatusCode,
        ArgumentException => (int)HttpStatusCode.BadRequest,
        ForbiddenException => (int)HttpStatusCode.Forbidden,
        // 401 makes the frontend clear the session and redirect to login, so only use it when the caller is
        // not authenticated. An authenticated caller lacking permission gets 403.
        UnauthorizedAccessException => context.User.Identity?.IsAuthenticated == true
            ? (int)HttpStatusCode.Forbidden
            : (int)HttpStatusCode.Unauthorized,
        _ => (int)HttpStatusCode.InternalServerError
    };
}
