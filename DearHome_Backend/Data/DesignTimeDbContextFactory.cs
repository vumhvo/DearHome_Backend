using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DearHome_Backend.Data;

/// <summary>
/// Used only by the <c>dotnet ef</c> tools (migrations add/script, database update). It builds the context directly
/// so the tools do not run <c>Program.cs</c>, which requires Jwt:Key and a Firebase credential file.
/// </summary>
/// <remarks>
/// Connection string lookup order: the <c>ConnectionStrings__DefaultConnection</c> environment variable, then
/// <c>ConnectionStrings:DefaultConnection</c> in appsettings.Development.json / appsettings.json (if present),
/// then a localhost placeholder. Adding a migration or generating a script never opens a connection, so the
/// placeholder is enough for those commands; <c>database update</c> needs a real connection string.
/// </remarks>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DearHomeContext>
{
    private const string ConnectionStringName = "DefaultConnection";
    private const string PlaceholderConnectionString =
        "Server=localhost,1433;Database=INTERIOR_SHOPPING;Integrated Security=False;TrustServerCertificate=True";

    public DearHomeContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        var options = new DbContextOptionsBuilder<DearHomeContext>()
            .UseSqlServer(connectionString)
            // Surface model warnings (e.g. decimal columns without precision) when running the tools.
            // stderr, so `dotnet ef migrations script` without -o still writes clean SQL to stdout.
            .LogTo(Console.Error.WriteLine, LogLevel.Warning)
            .Options;

        return new DearHomeContext(options);
    }

    private static string ResolveConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable($"ConnectionStrings__{ConnectionStringName}");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        // dotnet ef runs with the project directory as the working directory.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var fromSettings = configuration.GetConnectionString(ConnectionStringName);
        return string.IsNullOrWhiteSpace(fromSettings) ? PlaceholderConnectionString : fromSettings;
    }
}
