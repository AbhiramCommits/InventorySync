using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InventorySync.Infrastructure.Data;

public class SyncDbContextFactory : IDesignTimeDbContextFactory<SyncDbContext>
{
    private const string ConnectionString =
        "Server=localhost,1433;Database=InventorySync;User Id=sa;Password=InventorySync!Passw0rd;TrustServerCertificate=True";

    public SyncDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SyncDbContext>();
        optionsBuilder.UseSqlServer(
            ConnectionString,
            sql => sql.MigrationsAssembly(typeof(SyncDbContext).Assembly.FullName));

        return new SyncDbContext(optionsBuilder.Options);
    }
}
