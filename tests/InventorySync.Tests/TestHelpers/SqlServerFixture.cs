using DotNet.Testcontainers.Builders;

using InventorySync.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

using Testcontainers.MsSql;

namespace InventorySync.Tests.TestHelpers;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("InventorySync!Passw0rd")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public SyncDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SyncDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new SyncDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
