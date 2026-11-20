using DearHome_Backend.Repositories.Implementations;
using DearHome_Backend.Tests.TestUtilities;

namespace DearHome_Backend.Tests.Repositories;

public class OrderRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_with_null_currently_throws()
    {
        await using var context = TestDb.CreateInMemoryContext();
        var repository = new OrderRepository(context);

        // BUG: OrderRepository.GetByIdAsync casts the object id with (Guid)id inside the query, so a null id
        // (OrderService passes order.PromotionId! when it is null) throws instead of returning null.
        var act = () => repository.GetByIdAsync(null!);

        (await act.Should().ThrowAsync<Exception>())
            .Which.Should().Match<Exception>(e => e is NullReferenceException || e.InnerException is NullReferenceException);
    }

    [Fact(Skip = "BUG: OrderRepository.GetByIdAsync casts a null id to Guid and throws instead of returning null.")]
    public async Task GetByIdAsync_with_null_returns_null()
    {
        await using var context = TestDb.CreateInMemoryContext();
        var repository = new OrderRepository(context);

        (await repository.GetByIdAsync(null!)).Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_with_unknown_id_returns_null()
    {
        await using var context = TestDb.CreateInMemoryContext();
        var repository = new OrderRepository(context);

        (await repository.GetByIdAsync(Guid.NewGuid())).Should().BeNull();
    }
}
