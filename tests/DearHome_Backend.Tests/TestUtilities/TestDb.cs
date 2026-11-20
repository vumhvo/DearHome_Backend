using DearHome_Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace DearHome_Backend.Tests.TestUtilities;

/// <summary>Creates <see cref="DearHomeContext"/> instances for tests. Nothing here opens a database connection.</summary>
internal static class TestDb
{
    /// <summary>A context on a fresh, isolated EF Core InMemory database.</summary>
    public static DearHomeContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<DearHomeContext>()
            .UseInMemoryDatabase($"DearHomeTests-{Guid.NewGuid()}")
            .Options;
        return new DearHomeContext(options);
    }

    /// <summary>
    /// A SQL Server-configured context, used only to inspect the relational model and migrations.
    /// The connection string points nowhere; none of the callers open a connection.
    /// </summary>
    public static DearHomeContext CreateSqlServerModelContext()
    {
        var options = new DbContextOptionsBuilder<DearHomeContext>()
            .UseSqlServer("Server=localhost,1;Database=DearHomeModelOnly;Integrated Security=False;TrustServerCertificate=True")
            .Options;
        return new DearHomeContext(options);
    }
}
