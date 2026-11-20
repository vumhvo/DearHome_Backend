using Net.payOS.Types;

namespace DearHome_Backend.Services.External;

/// <summary>
/// Thin wrapper around the PayOS SDK so payment business logic does not depend on the SDK or its configuration.
/// </summary>
public interface IPayOSClient
{
    Task<CreatePaymentResult> CreatePaymentLinkAsync(PaymentData paymentData);
    Task<PaymentLinkInformation> GetPaymentLinkInformationAsync(long orderCode);
    Task<PaymentLinkInformation> CancelPaymentLinkAsync(long orderCode);
    WebhookData VerifyPaymentWebhookData(WebhookType body);
}
