using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InventorySync.Infrastructure.Data;

/// <summary>
/// Design-time factory used by EF Core tools (migrations) when no application host is running.
/// </summary>
[ExcludeFromCodeCoverage]
public class SyncDbContextFactory : IDesignTimeDbContextFactory<SyncDbContext>
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__DefaultConnection";

    private const string LocalPlaceholder =
        "Server=localhost,1433;Database=InventorySync;User Id=sa;Password=;TrustServerCertificate=True";

    /// <summary>
    /// Creates a context for design-time tooling.
    /// </summary>
    public SyncDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = LocalPlaceholder;
        }

        var optionsBuilder = new DbContextOptionsBuilder<SyncDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            sql => sql.MigrationsAssembly(typeof(SyncDbContext).Assembly.FullName));

        return new SyncDbContext(optionsBuilder.Options);
    }
}
