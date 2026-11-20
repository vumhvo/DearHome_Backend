using System.Security.Claims;
using System.Text.Json;
using DearHome_Backend.Exceptions;
using DearHome_Backend.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DearHome_Backend.Tests.Middlewares;

public class ErrorHandlerMiddlewareTests
{
    private const string GenericMessage = "An unexpected error occurred. Please try again later.";

    private static async Task<(HttpContext Context, JsonElement? Body)> InvokeAsync(
        Exception? exception,
        string environment = "Production",
        bool authenticated = false,
        bool requestAborted = false)
    {
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns(environment);

        var middleware = new ErrorHandlerMiddleware(
            _ => exception == null ? Task.CompletedTask : Task.FromException(exception),
            NullLogger<ErrorHandlerMiddleware>.Instance,
            hostEnvironment.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        if (authenticated)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "jane") }, "Test"));
        }
        if (requestAborted)
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            context.RequestAborted = cts.Token;
        }

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        JsonElement? body = string.IsNullOrEmpty(text) ? null : JsonDocument.Parse(text).RootElement.Clone();
        return (context, body);
    }

    private static Exception CreateException(string kind) => kind switch
    {
        nameof(NotFoundException) => new NotFoundException("Order not found."),
        nameof(KeyNotFoundException) => new KeyNotFoundException("Key not found."),
        nameof(BusinessRuleException) => new BusinessRuleException("Invalid verification code."),
        nameof(ArgumentException) => new ArgumentException("Bad argument."),
        nameof(ArgumentNullException) => new ArgumentNullException("id", "Value cannot be null."),
        nameof(BadHttpRequestException) => new BadHttpRequestException("Payload too large.", 413),
        nameof(ForbiddenException) => new ForbiddenException("Not your order."),
        nameof(UnauthorizedAccessException) => new UnauthorizedAccessException("Please log in."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    [Theory]
    [InlineData(nameof(NotFoundException), 404)]
    [InlineData(nameof(KeyNotFoundException), 404)]
    [InlineData(nameof(BusinessRuleException), 400)]
    [InlineData(nameof(ArgumentException), 400)]
    [InlineData(nameof(ArgumentNullException), 400)]
    [InlineData(nameof(BadHttpRequestException), 413)]
    [InlineData(nameof(ForbiddenException), 403)]
    [InlineData(nameof(UnauthorizedAccessException), 401)]
    public async Task Client_errors_map_to_status_code_and_expose_the_message(string exceptionKind, int expectedStatus)
    {
        var exception = CreateException(exceptionKind);

        var (context, body) = await InvokeAsync(exception);

        context.Response.StatusCode.Should().Be(expectedStatus);
        context.Response.ContentType.Should().Be("application/json");
        body!.Value.GetProperty("statusCode").GetInt32().Should().Be(expectedStatus);
        body.Value.GetProperty("message").GetString().Should().Be(exception.Message);
    }

    [Fact]
    public async Task Production_body_has_exactly_message_and_statusCode()
    {
        var (_, body) = await InvokeAsync(new NotFoundException("Missing."));

        body!.Value.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("message", "statusCode");
    }

    [Fact]
    public async Task UnauthorizedAccessException_for_an_authenticated_caller_is_403()
    {
        var (context, body) = await InvokeAsync(new UnauthorizedAccessException("Not allowed."), authenticated: true);

        context.Response.StatusCode.Should().Be(403);
        body!.Value.GetProperty("statusCode").GetInt32().Should().Be(403);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Server_errors_hide_the_message_outside_Development(string environment)
    {
        var (context, body) = await InvokeAsync(new InvalidOperationException("Connection string 'secret' is invalid."), environment);

        context.Response.StatusCode.Should().Be(500);
        body!.Value.GetProperty("message").GetString().Should().Be(GenericMessage);
        body.Value.GetProperty("statusCode").GetInt32().Should().Be(500);
        body.Value.TryGetProperty("detail", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Server_errors_show_message_and_detail_in_Development()
    {
        var (context, body) = await InvokeAsync(new Exception("Boom"), "Development");

        context.Response.StatusCode.Should().Be(500);
        body!.Value.GetProperty("message").GetString().Should().Be("Boom");
        body.Value.GetProperty("detail").GetString().Should().Contain("System.Exception: Boom");
    }

    [Fact]
    public async Task Successful_request_passes_through_untouched()
    {
        var (context, body) = await InvokeAsync(null);

        context.Response.StatusCode.Should().Be(200);
        body.Should().BeNull();
    }

    [Fact]
    public async Task Request_cancelled_by_the_client_writes_no_error_body()
    {
        var (context, body) = await InvokeAsync(new OperationCanceledException(), requestAborted: true);

        context.Response.StatusCode.Should().Be(200);
        body.Should().BeNull();
    }

    [Fact]
    public async Task OperationCanceledException_without_client_abort_is_a_server_error()
    {
        var (context, _) = await InvokeAsync(new OperationCanceledException());

        context.Response.StatusCode.Should().Be(500);
    }
}
