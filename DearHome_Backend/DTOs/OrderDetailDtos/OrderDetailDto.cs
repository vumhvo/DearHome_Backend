using DearHome_Backend.DTOs.ReviewDtos;
using DearHome_Backend.DTOs.VariantDtos;

namespace DearHome_Backend.DTOs.OrderDetailDtos;

public class OrderDetailDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } = 0;
    public decimal TotalPrice { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual VariantDto? Variant { get; set; }
    public virtual ICollection<ReviewSummaryDto>? Reviews { get; set; }
}
