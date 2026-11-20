using DearHome_Backend.DTOs.VariantDtos;

namespace DearHome_Backend.DTOs.ReviewDtos;

/// <summary>
/// A review as shown publicly on a product page (GET api/Review/product/{productId}).
/// Keeps the review.orderDetail.order.user nesting the frontend reads, but exposes only the
/// reviewer's public profile (id, name, avatar) instead of the full user record.
/// </summary>
public class ProductReviewDto : ReviewSummaryDto
{
    public ProductReviewOrderDetailDto? OrderDetail { get; set; }
}

public class ProductReviewOrderDetailDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public VariantDto? Variant { get; set; }
    public ProductReviewOrderDto? Order { get; set; }
}

public class ProductReviewOrderDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ReviewerDto? User { get; set; }
}

public class ReviewerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
}
