using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockDesk.Infrastructure.Persistence;

/// <summary>Lets the EF Core tools build the context without starting the web app.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<StockDeskDbContext>
{
    public StockDeskDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__StockDesk")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=StockDesk;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<StockDeskDbContext>().UseSqlServer(connectionString).Options;
        return new StockDeskDbContext(options);
    }
}
