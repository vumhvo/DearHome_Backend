using System;

namespace DearHome_Backend.DTOs.OrderDetailDtos;

public class AddOrderDetailDto
{
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
}
