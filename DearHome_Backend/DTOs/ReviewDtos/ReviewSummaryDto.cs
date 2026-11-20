namespace DearHome_Backend.DTOs.ReviewDtos;

/// <summary>
/// Review fields without navigation properties, used when a review is nested inside another resource
/// (e.g. OrderDetailDto.Reviews).
/// </summary>
public class ReviewSummaryDto
{
    public Guid Id { get; set; }
    public decimal Rating { get; set; }
    public string? ReviewText { get; set; }
    public Guid OrderDetailId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
