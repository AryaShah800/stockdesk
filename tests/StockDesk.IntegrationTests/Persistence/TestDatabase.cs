using Microsoft.EntityFrameworkCore;
using StockDesk.Application.Abstractions;
using StockDesk.Domain.Entities;
using StockDesk.Infrastructure.Persistence;

namespace StockDesk.IntegrationTests.Persistence;

/// <summary>
/// A throwaway LocalDB database built from the real migrations (trigger and procedures included),
/// dropped when disposed.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    public TestDatabase()
    {
        ConnectionString =
            $"Server=(localdb)\\MSSQLLocalDB;Database=StockDesk_Test_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public string ConnectionString { get; }

    public StockDeskDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<StockDeskDbContext>().UseSqlServer(ConnectionString).Options);

    public IUnitOfWork CreateUnitOfWork(StockDeskDbContext db) => new UnitOfWork(db);

    /// <summary>Adds a category and products, returning the products with their ids.</summary>
    public async Task<Product[]> SeedProductsAsync(params (string Sku, decimal Price, int Stock, int Reorder)[] items)
    {
        await using var db = CreateContext();
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == "General") ?? new Category("General");
        if (category.Id == 0)
        {
            db.Categories.Add(category);
            await db.SaveChangesAsync();
        }

        var products = items.Select(i => new Product(i.Sku, $"Product {i.Sku}", category.Id, null, i.Price / 2, i.Price, i.Stock, i.Reorder)).ToArray();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        return products;
    }

    public async Task<int> StockOfAsync(int productId)
    {
        await using var db = CreateContext();
        return await db.Products.Where(p => p.Id == productId).Select(p => p.QuantityInStock).SingleAsync();
    }

    public void Dispose()
    {
        using var db = CreateContext();
        db.Database.EnsureDeleted();
    }
}
