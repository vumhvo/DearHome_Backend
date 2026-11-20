using DearHome_Backend.Data;
using DearHome_Backend.Models;
using DearHome_Backend.Repositories.Implementations;
using DearHome_Backend.Tests.TestUtilities;

namespace DearHome_Backend.Tests.Repositories;

/// <summary>
/// Paging, sorting, search and filter of <see cref="BaseRepository{T}"/>, exercised through a real repository
/// (<see cref="PlacementRepository"/>, which does not override the paged queries) on EF Core InMemory.
/// </summary>
public class BaseRepositoryPaginationTests : IDisposable
{
    private const int SeededCount = 25;

    private readonly DearHomeContext _context;
    private readonly PlacementRepository _repository;

    public BaseRepositoryPaginationTests()
    {
        _context = TestDb.CreateInMemoryContext();
        // Names "Placement 01" .. "Placement 25"; every third one has no description.
        for (var i = 1; i <= SeededCount; i++)
        {
            _context.Placements.Add(new Placement
            {
                Id = Guid.NewGuid(),
                Name = $"Placement {i:00}",
                Description = i % 3 == 0 ? null : $"Description {i}"
            });
        }
        _context.Placements.Add(new Placement { Id = Guid.NewGuid(), Name = "Living room sofa corner", Description = "x" });
        _context.SaveChanges();
        _repository = new PlacementRepository(_context);
    }

    private const int TotalCount = SeededCount + 1;

    public void Dispose() => _context.Dispose();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Limit_below_one_falls_back_to_the_default_page_size(int limit)
    {
        var result = await _repository.GetAllAsync(0, limit, null, null, null);

        result.PageSize.Should().Be(10);
        result.Data.Should().HaveCount(10);
        result.TotalRecords.Should().Be(TotalCount);
    }

    [Fact]
    public async Task Limit_above_the_maximum_is_capped_at_100()
    {
        var result = await _repository.GetAllAsync(0, 1000, null, null, null);

        result.PageSize.Should().Be(100);
        result.Data.Should().HaveCount(TotalCount);
    }

    [Fact]
    public async Task Negative_offset_is_treated_as_zero()
    {
        var result = await _repository.GetAllAsync(-20, 10, null, null, "name");

        result.PageNumber.Should().Be(1);
        result.Data.First().Name.Should().Be("Living room sofa corner");
    }

    [Theory]
    [InlineData(0, 10, 1, 10)]
    [InlineData(10, 10, 2, 10)]
    [InlineData(20, 10, 3, 6)]
    [InlineData(30, 10, 4, 0)]
    [InlineData(5, 10, 1, 10)]
    public async Task PageNumber_is_offset_divided_by_limit_plus_one(int offset, int limit, int expectedPage, int expectedItems)
    {
        var result = await _repository.GetAllAsync(offset, limit, null, null, "name");

        result.PageNumber.Should().Be(expectedPage);
        result.Data.Should().HaveCount(expectedItems);
        result.TotalRecords.Should().Be(TotalCount);
    }

    [Fact]
    public async Task TotalPages_is_currently_never_set_by_BaseRepository()
    {
        var result = await _repository.GetAllAsync(0, 10, null, null, "name");

        // BUG: BaseRepository builds PaginatedResult with an object initializer and never sets TotalPages,
        // so it is always 0 and HasNextPage is always false. See the skipped test below for the expected value.
        result.TotalPages.Should().Be(0);
        result.HasNextPage.Should().BeFalse();
    }

    [Fact(Skip = "BUG: BaseRepository.GetAllAsync never sets PaginatedResult.TotalPages (always 0, HasNextPage always false).")]
    public async Task TotalPages_is_total_records_divided_by_page_size_rounded_up()
    {
        var result = await _repository.GetAllAsync(0, 10, null, null, "name");

        result.TotalPages.Should().Be(3);
        result.HasNextPage.Should().BeTrue();
    }

    [Theory]
    [InlineData("name")]
    [InlineData("Name")]
    [InlineData("NAME")]
    [InlineData("  name  ")]
    public async Task SortBy_matches_property_names_case_insensitively(string sortBy)
    {
        var result = await _repository.GetAllAsync(0, 100, null, null, sortBy);

        result.Data.Select(p => p.Name).Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task SortBy_descending()
    {
        var result = await _repository.GetAllAsync(0, 100, null, null, "name", isDescending: true);

        result.Data.Select(p => p.Name).Should().BeInDescendingOrder(StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("doesNotExist")]
    [InlineData("Name; DROP TABLE Placements")]
    [InlineData("Products")] // navigation, not a scalar property
    [InlineData("")]
    public async Task Unknown_sortBy_is_ignored(string sortBy)
    {
        var result = await _repository.GetAllAsync(0, 100, null, null, sortBy);

        result.Data.Should().HaveCount(TotalCount);
    }

    [Fact]
    public async Task Search_matches_names_containing_the_text()
    {
        var result = await _repository.GetAllAsync(0, 10, "sofa", null, null);

        result.Data.Should().ContainSingle().Which.Name.Should().Be("Living room sofa corner");
        result.TotalRecords.Should().Be(1);
    }

    [Fact]
    public async Task Filter_keeps_only_rows_where_the_property_is_not_null()
    {
        var result = await _repository.GetAllAsync(0, 100, null, "description", null);

        result.Data.Should().OnlyContain(p => p.Description != null);
        result.TotalRecords.Should().Be(TotalCount - SeededCount / 3);
    }

    [Theory]
    [InlineData("doesNotExist")]
    [InlineData("CreatedAt")] // non-nullable value type: nothing to filter
    public async Task Filter_on_unknown_or_non_nullable_property_is_ignored(string filter)
    {
        var result = await _repository.GetAllAsync(0, 100, null, filter, null);

        result.TotalRecords.Should().Be(TotalCount);
    }

    [Fact]
    public async Task Simple_overload_also_normalizes_paging()
    {
        var result = await _repository.GetAllAsync(-3, 0, null);

        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Data.Should().HaveCount(10);
    }
}
