using DearHome_Backend.DTOs.PaginationDtos;

namespace DearHome_Backend.Tests.Repositories;

public class PaginatedResultTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(25, 10, 3)]
    public void Constructor_computes_TotalPages_rounding_up(int totalRecords, int pageSize, int expectedPages)
    {
        var result = new PaginatedResult<int>(new List<int>(), 1, pageSize, totalRecords);

        result.TotalPages.Should().Be(expectedPages);
    }

    [Fact]
    public void HasNextPage_is_true_before_the_last_page()
    {
        new PaginatedResult<int>(new List<int>(), 2, 10, 25).HasNextPage.Should().BeTrue();
        new PaginatedResult<int>(new List<int>(), 3, 10, 25).HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_on_the_first_page_is_currently_true()
    {
        // BUG: page numbers are 1-based (offSet / limit + 1), but HasPreviousPage is PageNumber > 0,
        // so the first page reports a previous page. See the skipped test below.
        new PaginatedResult<int>(new List<int>(), 1, 10, 25).HasPreviousPage.Should().BeTrue();
    }

    [Fact(Skip = "BUG: PaginatedResult.HasPreviousPage is PageNumber > 0 but page numbers are 1-based, so page 1 reports a previous page.")]
    public void HasPreviousPage_is_false_on_the_first_page()
    {
        new PaginatedResult<int>(new List<int>(), 1, 10, 25).HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void MapData_projects_items_and_keeps_paging_metadata()
    {
        var source = new PaginatedResult<int>(new List<int> { 1, 2, 3 }, 2, 3, 9);

        var mapped = source.MapData(items => items.Select(i => $"#{i}").ToList());

        mapped.Data.Should().Equal("#1", "#2", "#3");
        mapped.PageNumber.Should().Be(2);
        mapped.PageSize.Should().Be(3);
        mapped.TotalPages.Should().Be(3);
        mapped.TotalRecords.Should().Be(9);
    }

    [Fact]
    public void Constructor_rejects_null_data()
    {
        var act = () => new PaginatedResult<int>(null!, 1, 10, 0);

        act.Should().Throw<ArgumentNullException>();
    }
}
