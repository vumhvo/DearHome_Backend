using System;
using DearHome_Backend.DTOs.PaginationDtos;
using DearHome_Backend.Models;

namespace DearHome_Backend.Services.Interfaces;

public interface IPromotionService : IBaseService<Promotion>
{
    Task<IEnumerable<Promotion>> GetUsablePromotionByUserId(Guid userId);
}
