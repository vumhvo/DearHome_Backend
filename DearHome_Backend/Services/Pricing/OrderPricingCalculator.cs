namespace DearHome_Backend.Services.Pricing;

/// <summary>
/// Pure order pricing rules: no I/O, no entity mutation, so every rule can be unit tested in isolation.
/// <see cref="Implementations.OrderService"/> loads the inputs (variants, promotion, shipping fee) and applies the results.
/// </summary>
public static class OrderPricingCalculator
{
    /// <summary>Unit price of a variant: the product's base price plus the variant's price adjustment.</summary>
    public static decimal CalculateUnitPrice(decimal productPrice, decimal priceAdjustment)
        => priceAdjustment + productPrice;

    /// <summary>Line total: unit price times quantity.</summary>
    public static decimal CalculateLineTotal(decimal unitPrice, int quantity)
        => unitPrice * quantity;

    /// <summary>Order subtotal (the order's <c>TotalPrice</c>): the sum of the line totals.</summary>
    public static decimal CalculateSubtotal(IEnumerable<decimal> lineTotals)
    {
        ArgumentNullException.ThrowIfNull(lineTotals);

        decimal subtotal = 0;
        foreach (var lineTotal in lineTotals)
        {
            subtotal += lineTotal;
        }
        return subtotal;
    }

    /// <summary>Discount amount for a percentage discount applied to the subtotal.</summary>
    public static decimal CalculateDiscount(decimal subtotal, decimal discountPercentage)
        => subtotal * discountPercentage / 100;

    /// <summary>Amount the customer pays: subtotal minus the applied discount plus the shipping fee.</summary>
    public static decimal CalculateFinalPrice(decimal subtotal, decimal appliedDiscount, decimal shippingFee)
        => subtotal - appliedDiscount + shippingFee;
}
