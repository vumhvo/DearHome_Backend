using System;

namespace DearHome_Backend.DTOs.PaginationDtos;

public class PaginatedResult<T>
{
    public List<T> Data { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }   
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
    public bool HasPreviousPage => PageNumber > 0;
    public bool HasNextPage => PageNumber < TotalPages;

    public PaginatedResult(List<T> data, int pageNumber, int pageSize, int totalRecords)
    {
        this.Data = data ?? throw new ArgumentNullException(nameof(data));
        this.PageNumber = pageNumber;
        this.PageSize = pageSize;
        this.TotalRecords = totalRecords;
        this.TotalPages = totalRecords > 0 
            ? (int)Math.Ceiling((double)totalRecords / pageSize) 
            : 0;
    }

    public PaginatedResult()
    {
        Data = default!;
        PageNumber = 0;
        PageSize = 0;
        TotalPages = 0;
        TotalRecords = 0;
    }

    /// <summary>
    /// Projects the page items (e.g. entities to DTOs) while keeping the paging metadata unchanged.
    /// </summary>
    public PaginatedResult<TResult> MapData<TResult>(Func<List<T>, List<TResult>> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return new PaginatedResult<TResult>
        {
            Data = Data is null ? default! : map(Data),
            PageNumber = PageNumber,
            PageSize = PageSize,
            TotalPages = TotalPages,
            TotalRecords = TotalRecords
        };
    }
}
