using System;
using System.Threading.Tasks;
using DearHome_Backend.Models;
using DearHome_Backend.Services.Interfaces;
using DearHome_Backend.Services.External;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
using Net.payOS.Types;
using System.Collections.Generic;
using DearHome_Backend.DTOs.PaymentDto;
using DearHome_Backend.Repositories.Interfaces;
using DearHome_Backend.Constants;

namespace DearHome_Backend.Services.Implementations;

public class PaymentService : IPaymentService
{
    private readonly IPayOSClient _payOSClient;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<PaymentService> _logger;
    private readonly TelemetryClient _telemetryClient;

    public PaymentService(
        IPayOSClient payOSClient,
        ILogger<PaymentService> logger,
        TelemetryClient telemetryClient,
        IOrderRepository orderRepository)
    {
        _payOSClient = payOSClient ?? throw new ArgumentNullException(nameof(payOSClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<PaymentLinkInformation> CancelPaymentLinkAsync(long orderId)
    {
        using var operation = _telemetryClient.StartOperation<Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry>("CancelPaymentLink");
        try
        {
            _logger.LogInformation("Cancelling payment link for order ID: {orderId}", orderId);
            var response = await _payOSClient.CancelPaymentLinkAsync(orderId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel payment link for order ID: {orderId}", orderId);
            throw;
        }
    }

    public async Task<CreatePaymentResult> CreatePaymentLinkAsync(Order order, User user, string url)
    {
        using var operation = _telemetryClient.StartOperation<Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry>("CreatePaymentLink");
        try
        {
            _logger.LogInformation("Creating payment link for order {OrderId}", order.Id);

            // Create payment request
            long orderCode = order.Id.ToString().GetHashCode() & 0x7FFFFFFF; // Convert Guid to positive long integer
            int amount = (int)order.FinalPrice; // Convert to smallest currency unit (cents)
            string description = $"Payment for order";
            var items = MapOrderItemsToPayOSItems(order);
            string buyerName = user.Name;
            string buyerEmail = user.Email!;
            string buyerPhone = user.PhoneNumber ?? string.Empty;
            string buyerAddress = order.Address != null ? 
                $"{order.Address.Street}, {order.Address.District}, {order.Address.City}" : string.Empty;
            string returnUrl = $"{url}#/verify-payment";
            string cancelUrl = $"{url}#/verify-payment";
            string extraData = $"Order ID: {order.Id}, User ID: {user.Id}";
            long expiredAt = new DateTimeOffset(DateTime.UtcNow.AddHours(24)).ToUnixTimeSeconds();
            string signature = user.Name; 

            var paymentData = new PaymentData(
                orderCode, 
                amount, 
                description, 
                items, 
                cancelUrl, 
                returnUrl, 
                signature, 
                buyerName, 
                buyerEmail, 
                buyerPhone, 
                buyerAddress, 
                expiredAt
            );

            // Create payment link
            var response = await _payOSClient.CreatePaymentLinkAsync(paymentData);
            
            // Log success and payment details
            _logger.LogInformation("Payment link created successfully for order {OrderId}, checkout URL: {CheckoutUrl}", 
                order.Id, response.checkoutUrl);

            // Track custom event for business metrics
            _telemetryClient.TrackEvent("PaymentLinkCreated", new Dictionary<string, string>
            {
                { "OrderId", order.Id.ToString() },
                { "PaymentId", response.paymentLinkId },
                { "Amount", (order.FinalPrice).ToString() }
            });

            return response;
        }
        catch (Exception ex)
        {
            // Log exception details
            _logger.LogError(ex, "Failed to create payment link for order {OrderId}", order.Id);
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                { "OrderId", order.Id.ToString() },
                { "Amount", order.FinalPrice.ToString() }
            });
            throw;
        }
    }

    public async Task<PaymentLinkInformation> GetPaymentLinkInformationAsync(long orderId)
    {
        using var operation = _telemetryClient.StartOperation<Microsoft.ApplicationInsights.DataContracts.DependencyTelemetry>("CheckPaymentStatus");
        try
        {
            _logger.LogInformation("Checking payment status for order ID: {orderId}", orderId);
            
            var paymentData = await _payOSClient.GetPaymentLinkInformationAsync(orderId);
            
            _logger.LogInformation("Payment status for {PaymentLinkId}: {Status}", 
                orderId, paymentData.status);
                
            return paymentData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check payment status for order ID: {orderId}", orderId);
            _telemetryClient.TrackException(ex);
            throw;
        }
    }

    public async Task<PaymentLinkInformation> VerifyPaymentAsync(VerifyPaymentDto verifyPaymentDto)
    {
        if (verifyPaymentDto.Status == "PAID") {
            // Update order status to "Paid"
            await _orderRepository.UpdateOrderStatusByPaymentOrderCodeAsync(verifyPaymentDto.OrderCode, OrderStatus.Processing);
        }
        else if (verifyPaymentDto.Status == "CANCELLED")
        {
            // Update order status to "Cancelled"
            await _orderRepository.UpdateOrderStatusByPaymentOrderCodeAsync(verifyPaymentDto.OrderCode, OrderStatus.Cancelled);
        }

        return await GetPaymentLinkInformationAsync(verifyPaymentDto.OrderCode);
    }

    public bool VerifyWebhookSignature(WebhookType body)
    {
        try
        {
            var webhookData = _payOSClient.VerifyPaymentWebhookData(body);
            return webhookData != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify webhook signature");
            return false;
        }
    }

    private List<ItemData> MapOrderItemsToPayOSItems(Order order)
    {
        var items = new List<ItemData>();
        
        if (order.OrderDetails != null)
        {
            foreach (var orderItem in order.OrderDetails)
            {
                if (orderItem.Variant != null && orderItem.Variant.Product != null)
                {
                    items.Add(new ItemData(
                        orderItem.Variant.Product.Name,
                        (int)(orderItem.Variant.PriceAdjustment + orderItem.Variant.Product.Price),
                        orderItem.Quantity
                    ));
                }
                else
                {
                    _logger.LogWarning("Skipping order item with missing variant or product data for order {OrderId}", order.Id);
                }
            }
        }
        
        return items;
    }
}
