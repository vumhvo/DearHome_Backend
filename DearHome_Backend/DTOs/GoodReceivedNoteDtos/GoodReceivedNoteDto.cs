using System;
using DearHome_Backend.Constants;
using DearHome_Backend.DTOs.GoodReceivedItemDtos;

namespace DearHome_Backend.DTOs.GoodReceivedNoteDtos;

public class GoodReceivedNoteDto
{
    public Guid Id { get; set; }
    public DateTime ReceivedDate { get; set; }
    public string? Note { get; set; }
    public int Quantity { get; set; }
    public decimal TotalCost { get; set; } = 0;
    public decimal ShippingCost { get; set; } = 0;
    public GoodReceivedNoteStatus Status { get; set; } = GoodReceivedNoteStatus.Received;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual ICollection<GoodReceivedItemDto>? GoodReceivedItems { get; set; } = new List<GoodReceivedItemDto>();
}
