using DearHome_Backend.Config;
using Microsoft.Extensions.Options;
using Net.payOS;
using Net.payOS.Types;

namespace DearHome_Backend.Services.External;

/// <summary>
/// <see cref="IPayOSClient"/> backed by the PayOS SDK, configured from <see cref="PayOSOptions"/>.
/// </summary>
public class PayOSClient : IPayOSClient
{
    private readonly PayOS _payOS;

    public PayOSClient(IOptions<PayOSOptions> options, ILogger<PayOSClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var settings = options.Value;
        if (string.IsNullOrEmpty(settings.ClientId) || string.IsNullOrEmpty(settings.ApiKey) || string.IsNullOrEmpty(settings.ChecksumKey))
        {
            logger.LogError("PayOS configuration is incomplete. Please check your configuration settings.");
            throw new InvalidOperationException("PayOS configuration is incomplete.");
        }

        _payOS = new PayOS(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
        logger.LogInformation("PayOS service initialized successfully");
    }

    public Task<CreatePaymentResult> CreatePaymentLinkAsync(PaymentData paymentData) => _payOS.createPaymentLink(paymentData);

    public Task<PaymentLinkInformation> GetPaymentLinkInformationAsync(long orderCode) => _payOS.getPaymentLinkInformation(orderCode);

    public Task<PaymentLinkInformation> CancelPaymentLinkAsync(long orderCode) => _payOS.cancelPaymentLink(orderCode);

    public WebhookData VerifyPaymentWebhookData(WebhookType body) => _payOS.verifyPaymentWebhookData(body);
}
