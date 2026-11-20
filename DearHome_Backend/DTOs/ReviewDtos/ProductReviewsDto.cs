using DearHome_Backend.DTOs.PaginationDtos;

namespace DearHome_Backend.DTOs.ReviewDtos;

public class ProductReviewsDto
{
    public Guid ProductId { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<decimal, int> RatingCounts { get; set; } = new Dictionary<decimal, int>();
    public PaginatedResult<ProductReviewDto> Reviews { get; set; } = new PaginatedResult<ProductReviewDto>();
}
