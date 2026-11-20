using DearHome_Backend.Services.Pricing;

namespace DearHome_Backend.Tests.Pricing;

public class OrderPricingCalculatorTests
{
    [Theory]
    [InlineData(100, 20, 120)]
    [InlineData(100, 0, 100)]
    [InlineData(100, -15, 85)]
    [InlineData(0, 0, 0)]
    [InlineData(1_250_000.50, 99.25, 1_250_099.75)]
    public void CalculateUnitPrice_adds_variant_adjustment_to_product_price(decimal productPrice, decimal adjustment, decimal expected)
    {
        OrderPricingCalculator.CalculateUnitPrice(productPrice, adjustment).Should().Be(expected);
    }

    [Theory]
    [InlineData(120, 3, 360)]
    [InlineData(120, 1, 120)]
    [InlineData(120, 0, 0)]
    [InlineData(19.99, 3, 59.97)]
    public void CalculateLineTotal_multiplies_unit_price_by_quantity(decimal unitPrice, int quantity, decimal expected)
    {
        OrderPricingCalculator.CalculateLineTotal(unitPrice, quantity).Should().Be(expected);
    }

    [Fact]
    public void CalculateSubtotal_sums_line_totals()
    {
        OrderPricingCalculator.CalculateSubtotal(new[] { 100m, 250.5m, 0m, 49.5m }).Should().Be(400m);
    }

    [Fact]
    public void CalculateSubtotal_of_no_lines_is_zero()
    {
        OrderPricingCalculator.CalculateSubtotal(Array.Empty<decimal>()).Should().Be(0m);
    }

    [Fact]
    public void CalculateSubtotal_rejects_null()
    {
        var act = () => OrderPricingCalculator.CalculateSubtotal(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(1000, 0, 0)]
    [InlineData(1000, 10, 100)]
    [InlineData(1000, 100, 1000)]
    [InlineData(999, 15, 149.85)]
    public void CalculateDiscount_is_a_percentage_of_the_subtotal(decimal subtotal, decimal percentage, decimal expected)
    {
        OrderPricingCalculator.CalculateDiscount(subtotal, percentage).Should().Be(expected);
    }

    [Fact]
    public void CalculateDiscount_does_not_round()
    {
        // 33.33...%: decimal arithmetic keeps the full precision; rounding happens only when the value is stored
        // in a decimal(18,2) column.
        var discount = OrderPricingCalculator.CalculateDiscount(100m, 100m / 3m);

        discount.Should().BeApproximately(33.3333333333m, 0.0000001m);
        decimal.Round(discount, 2).Should().Be(33.33m);
    }

    [Theory]
    [InlineData(1000, 100, 30, 930)]
    [InlineData(1000, 0, 0, 1000)]
    [InlineData(1000, 1000, 0, 0)]
    [InlineData(1000, 1000, 25, 25)]
    public void CalculateFinalPrice_is_subtotal_minus_discount_plus_shipping(decimal subtotal, decimal discount, decimal shipping, decimal expected)
    {
        OrderPricingCalculator.CalculateFinalPrice(subtotal, discount, shipping).Should().Be(expected);
    }

    [Fact]
    public void Full_order_pricing_pipeline()
    {
        // Two lines: (100 + 20) x 2 and (50 + 0) x 1, a 10% promotion and a 30 shipping fee.
        var line1 = OrderPricingCalculator.CalculateLineTotal(OrderPricingCalculator.CalculateUnitPrice(100m, 20m), 2);
        var line2 = OrderPricingCalculator.CalculateLineTotal(OrderPricingCalculator.CalculateUnitPrice(50m, 0m), 1);
        var subtotal = OrderPricingCalculator.CalculateSubtotal(new[] { line1, line2 });
        var discount = OrderPricingCalculator.CalculateDiscount(subtotal, 10m);
        var final = OrderPricingCalculator.CalculateFinalPrice(subtotal, discount, 30m);

        line1.Should().Be(240m);
        line2.Should().Be(50m);
        subtotal.Should().Be(290m);
        discount.Should().Be(29m);
        final.Should().Be(291m);
    }
}
