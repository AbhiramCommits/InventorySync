using InventorySync.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace InventorySync.Tests.TestHelpers;

internal static class InMemoryDb
{
    public static SyncDbContext Create()
    {
        var options = new DbContextOptionsBuilder<SyncDbContext>()
            .UseInMemoryDatabase($"inventorysync-tests-{Guid.NewGuid():N}")
            .Options;

        return new SyncDbContext(options);
    }
}
