using DearHome_Backend.Constants;
using DearHome_Backend.Data;
using DearHome_Backend.Models;
using DearHome_Backend.Tests.TestUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DearHome_Backend.Tests.Data;

/// <summary>Model and migration checks. They inspect the EF model only; no database connection is opened.</summary>
public class MigrationsTests
{
    private const string InitialCreate = "20261120171646_InitialCreate";

    // Seed data (HasData) is only kept in the design-time model, not in the read-optimized runtime model.
    private static IModel DesignModel(DbContext context) => context.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void InitialCreate_migration_is_present()
    {
        using var context = TestDb.CreateSqlServerModelContext();

        context.Database.GetMigrations().Should().StartWith(InitialCreate);
    }

    [Fact]
    public void Model_has_no_changes_missing_from_the_migrations()
    {
        using var context = TestDb.CreateSqlServerModelContext();

        // Fails when the model changed without `dotnet ef migrations add`, or when seed data is not deterministic.
        context.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Fact]
    public void Every_decimal_column_is_decimal_18_2()
    {
        using var context = TestDb.CreateSqlServerModelContext();

        var decimalColumns = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
            .ToList();

        decimalColumns.Should().NotBeEmpty();
        decimalColumns.Should().OnlyContain(p => p.GetColumnType() == "decimal(18,2)");
    }

    [Fact]
    public void Identity_tables_use_Guid_keys()
    {
        using var context = TestDb.CreateSqlServerModelContext();

        context.Model.FindEntityType(typeof(User))!.FindPrimaryKey()!.Properties.Single().ClrType.Should().Be(typeof(Guid));
        context.Model.FindEntityType(typeof(IdentityRole<Guid>))!.FindPrimaryKey()!.Properties.Single().ClrType.Should().Be(typeof(Guid));
    }

    [Fact]
    public void Seed_data_contains_admin_and_user_roles_and_an_admin_account()
    {
        using var context = TestDb.CreateSqlServerModelContext();

        var roles = DesignModel(context).FindEntityType(typeof(IdentityRole<Guid>))!.GetSeedData().ToList();
        var users = DesignModel(context).FindEntityType(typeof(User))!.GetSeedData().ToList();
        var userRoles = DesignModel(context).FindEntityType(typeof(IdentityUserRole<Guid>))!.GetSeedData().ToList();

        roles.Select(r => r[nameof(IdentityRole<Guid>.Name)]).Should().BeEquivalentTo(new[] { nameof(UserRole.Admin), nameof(UserRole.User) });
        var admin = users.Should().ContainSingle().Subject;
        admin[nameof(User.UserName)].Should().Be("admin");

        var adminRoleId = roles.Single(r => (string?)r[nameof(IdentityRole<Guid>.Name)] == nameof(UserRole.Admin))[nameof(IdentityRole<Guid>.Id)];
        userRoles.Should().ContainSingle(ur =>
            Equals(ur[nameof(IdentityUserRole<Guid>.UserId)], admin[nameof(User.Id)]) &&
            Equals(ur[nameof(IdentityUserRole<Guid>.RoleId)], adminRoleId));
    }

    [Fact]
    public void Seeded_admin_password_hash_verifies_against_the_documented_default_password()
    {
        using var context = TestDb.CreateSqlServerModelContext();
        var admin = DesignModel(context).FindEntityType(typeof(User))!.GetSeedData().Single();
        var hash = (string)admin[nameof(User.PasswordHash)]!;

        var result = new PasswordHasher<User>().VerifyHashedPassword(
            new User { Name = "Admin User", UserName = "admin" }, hash, "Admin@123");

        result.Should().Be(PasswordVerificationResult.Success);
    }

    [Fact]
    public void Seed_data_is_deterministic_across_model_builds()
    {
        // Two contexts with different options build the model twice; the seed values must be identical.
        using var first = TestDb.CreateSqlServerModelContext();
        using var second = TestDb.CreateInMemoryContext();

        var a = DesignModel(first).FindEntityType(typeof(User))!.GetSeedData().Single();
        var b = DesignModel(second).FindEntityType(typeof(User))!.GetSeedData().Single();

        a[nameof(User.Id)].Should().Be(b[nameof(User.Id)]);
        a[nameof(User.PasswordHash)].Should().Be(b[nameof(User.PasswordHash)]);
        a[nameof(User.ConcurrencyStamp)].Should().Be(b[nameof(User.ConcurrencyStamp)]);
    }
}

/// <summary>
/// Environment-variable based tests for <see cref="DesignTimeDbContextFactory"/>. They share process state, so they
/// live in one non-parallel collection.
/// </summary>
[CollectionDefinition(nameof(EnvironmentVariableCollection), DisableParallelization = true)]
public class EnvironmentVariableCollection;

[Collection(nameof(EnvironmentVariableCollection))]
public class DesignTimeDbContextFactoryTests
{
    private const string Variable = "ConnectionStrings__DefaultConnection";

    private static string? CreateAndGetConnectionString(string? environmentValue)
    {
        var previous = Environment.GetEnvironmentVariable(Variable);
        try
        {
            Environment.SetEnvironmentVariable(Variable, environmentValue);
            using var context = new DesignTimeDbContextFactory().CreateDbContext(Array.Empty<string>());
            context.Database.IsSqlServer().Should().BeTrue();
            return context.Database.GetConnectionString();
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, previous);
        }
    }

    [Fact]
    public void Uses_the_connection_string_from_the_environment()
    {
        const string fromEnvironment = "Server=db.example.test;Database=FromEnv;User Id=u;Password=p;TrustServerCertificate=True";

        CreateAndGetConnectionString(fromEnvironment).Should().Be(fromEnvironment);
    }

    [Fact]
    public void Falls_back_to_a_localhost_placeholder_without_configuration()
    {
        // A developer's local appsettings(.Development).json can be copied next to the test assembly; the
        // placeholder is only used when neither file is present (always the case in CI).
        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "appsettings.json")) || File.Exists(Path.Combine(cwd, "appsettings.Development.json")))
        {
            return;
        }

        CreateAndGetConnectionString(null).Should().Contain("localhost").And.Contain("INTERIOR_SHOPPING");
    }
}
