using System.Runtime.CompilerServices;
using DearHome_Backend.Constants;
using DearHome_Backend.Models;
using DearHome_Backend.Repositories.Interfaces;
using DearHome_Backend.Services.Implementations;
using DearHome_Backend.Services.Interfaces;
using Moq;
using Net.payOS.Types;

namespace DearHome_Backend.Tests.Services;

/// <summary>
/// Order pricing as applied by <see cref="OrderService"/> (CalculatedOrderAsync), observed through
/// <see cref="OrderService.AddOrderWithOnlinePaymentAsync"/>, which does not publish NATS events.
/// </summary>
public class OrderServicePricingTests
{
    private static readonly Guid VariantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IPaymentService> _paymentService = new();
    private readonly Mock<IShippingService> _shippingService = new(MockBehavior.Strict);
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IVariantRepository> _variantRepository = new();
    private Order? _orderSentToPayment;

    public OrderServicePricingTests()
    {
        _variantRepository.Setup(r => r.GetWithProductByIdAsync(VariantId)).ReturnsAsync(new Variant
        {
            Id = VariantId,
            Sku = "SOFA-GREY",
            ImageUrls = new List<string>(),
            PriceAdjustment = 20m,
            Product = new Product { Name = "Sofa", Description = "Grey sofa", Price = 100m }
        });
        _userRepository.Setup(r => r.GetByIdAsync(It.IsAny<object>()))
            .ReturnsAsync(new User { Id = UserId, Name = "Jane", UserName = "jane", Email = "jane@example.com" });

        // payOS result type has no public parameterless constructor; the service only copies three fields from it.
        var paymentResult = (CreatePaymentResult)RuntimeHelpers.GetUninitializedObject(typeof(CreatePaymentResult));
        _paymentService
            .Setup(s => s.CreatePaymentLinkAsync(It.IsAny<Order>(), It.IsAny<User>(), It.IsAny<string>()))
            .Callback<Order, User, string>((order, _, _) => _orderSentToPayment = order)
            .ReturnsAsync(paymentResult);
    }

    private OrderService CreateService()
    {
        // NatsService connects to NATS in its constructor; the online-payment path never publishes, so an
        // uninitialized instance is enough to satisfy the constructor's null check.
        var nats = (NatsService)RuntimeHelpers.GetUninitializedObject(typeof(NatsService));
        return new OrderService(_orderRepository.Object, _paymentService.Object, _shippingService.Object,
            _userRepository.Object, _variantRepository.Object, nats);
    }

    private static Order NewOrder(Guid? promotionId) => new()
    {
        UserId = UserId,
        AddressId = Guid.NewGuid(),
        PaymentMethod = PaymentMethods.BankTransfer,
        PromotionId = promotionId,
        OrderDetails = new List<OrderDetail> { new() { VariantId = VariantId, Quantity = 2 } }
    };

    [Fact]
    public async Task Line_prices_subtotal_and_final_price_are_computed_without_promotion()
    {
        await CreateService().AddOrderWithOnlinePaymentAsync(NewOrder(Guid.Empty), "https://shop.test/return");

        var order = _orderSentToPayment!;
        var line = order.OrderDetails!.Single();
        line.UnitPrice.Should().Be(120m);
        line.TotalPrice.Should().Be(240m);
        order.TotalPrice.Should().Be(240m);
        order.Discount.Should().Be(0m);
        order.FinalPrice.Should().Be(240m);
        order.PromotionId.Should().BeNull("an empty promotion id is normalized to null");
        _orderRepository.Verify(r => r.AddAsync(It.Is<Order>(o => o.FinalPrice == 240m)), Times.Once);
    }

    [Fact]
    public async Task Promotion_discount_currently_comes_from_an_Order_looked_up_by_the_promotion_id()
    {
        var promotionId = Guid.NewGuid();
        // BUG: OrderService.CalculatedOrderAsync calls _orderRepository.GetByIdAsync(promotionId) and uses that
        // *order's* Discount as the percentage, instead of loading the Promotion and using DiscountPercentage.
        _orderRepository.Setup(r => r.GetByIdAsync(promotionId)).ReturnsAsync(new Order { Id = promotionId, Discount = 50m });

        await CreateService().AddOrderWithOnlinePaymentAsync(NewOrder(promotionId), "https://shop.test/return");

        _orderSentToPayment!.Discount.Should().Be(120m); // 240 * 50 / 100
        _orderSentToPayment.FinalPrice.Should().Be(120m);
    }

    [Fact]
    public async Task Promotion_id_that_matches_no_order_currently_gives_no_discount()
    {
        // BUG (same as above): a real promotion id never matches an order id, so promotions never apply.
        await CreateService().AddOrderWithOnlinePaymentAsync(NewOrder(Guid.NewGuid()), "https://shop.test/return");

        _orderSentToPayment!.Discount.Should().Be(0m);
        _orderSentToPayment.FinalPrice.Should().Be(240m);
    }

    [Fact(Skip = "BUG: OrderService.CalculatedOrderAsync looks up an Order by the promotion id instead of the Promotion.")]
    public async Task Promotion_discount_is_never_looked_up_in_the_order_repository()
    {
        var promotionId = Guid.NewGuid();

        await CreateService().AddOrderWithOnlinePaymentAsync(NewOrder(promotionId), "https://shop.test/return");

        _orderRepository.Verify(r => r.GetByIdAsync(promotionId), Times.Never);
    }

    [Fact]
    public async Task Null_promotion_id_is_currently_passed_to_the_order_repository()
    {
        // BUG: only Guid.Empty is treated as "no promotion"; a null PromotionId falls into the lookup branch and calls
        // GetByIdAsync(null). The real OrderRepository then fails (see OrderRepositoryTests), so an order created
        // without a promotionId field fails unless the client sends Guid.Empty.
        await CreateService().AddOrderWithOnlinePaymentAsync(NewOrder(null), "https://shop.test/return");

        _orderRepository.Verify(r => r.GetByIdAsync(null!), Times.Once);
    }
}
