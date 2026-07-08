using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Store.Data;

/// <summary>
/// Design-time factory that lets <c>dotnet ef</c> build the model against <c>Store.Data</c> alone,
/// without the API host — handy when the running app locks <c>Store.Api</c>'s output DLLs. The
/// connection string is a placeholder: <c>migrations add</c> only inspects the model and never opens
/// a connection. At runtime the app still wires the real context via <c>AddStoreData(configuration)</c>.
/// </summary>
public sealed class StoreDbContextFactory : IDesignTimeDbContextFactory<StoreDbContext>
{
    public StoreDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=MyStore;Trusted_Connection=True;TrustServerCertificate=True;",
                sql => sql.MigrationsAssembly(typeof(StoreDbContext).Assembly.GetName().Name))
            .Options;

        return new StoreDbContext(options);
    }
}
