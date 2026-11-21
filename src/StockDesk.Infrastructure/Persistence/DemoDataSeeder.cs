using Microsoft.EntityFrameworkCore;
using StockDesk.Domain.Entities;

namespace StockDesk.Infrastructure.Persistence;

/// <summary>Adds a small sample catalogue to an empty database, for trying the app out locally.</summary>
public sealed class DemoDataSeeder(StockDeskDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Categories.AnyAsync(ct))
            return;

        var stationery = new Category("Stationery", "Pens, notebooks and office supplies");
        var grocery = new Category("Grocery", "Packaged food");
        var electrical = new Category("Electrical", "Bulbs, cables and batteries");
        var supplier = new Supplier("Shree Traders", "+91 79 0000 0000", "orders@example.com");
        db.AddRange(stationery, grocery, electrical, supplier);
        await db.SaveChangesAsync(ct);

        var products = new[]
        {
            new Product("PEN-BLU-01", "Ball pen, blue", stationery.Id, supplier.Id, 6.50m, 10.00m, 400, 50),
            new Product("NB-A5-200", "Notebook A5, 200 pages", stationery.Id, supplier.Id, 38.00m, 55.00m, 120, 15),
            new Product("RICE-BAS-1K", "Basmati rice 1 kg", grocery.Id, null, 95.00m, 120.00m, 90, 10),
            new Product("TEA-500", "Tea leaves 500 g", grocery.Id, null, 180.00m, 230.00m, 40, 10),
            new Product("LED-9W", "LED bulb 9 W", electrical.Id, supplier.Id, 72.00m, 99.00m, 60, 8),
            new Product("BAT-AA-4", "AA batteries, pack of 4", electrical.Id, supplier.Id, 60.00m, 85.00m, 30, 5),
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync(ct);
        var openedAt = DateTime.UtcNow.Date.AddDays(-14);
        db.StockMovements.AddRange(products.Select(p => new StockMovement(p, MovementType.Opening, p.QuantityInStock,
            p.QuantityInStock, "demo", openedAt, "Opening stock")));
        await db.SaveChangesAsync(ct);

        await SeedSalesHistoryAsync(products, ct);
    }

    /// <summary>
    /// Two weeks of bills, so the dashboard and reports have something to show. Each sale goes through the
    /// normal path, so the stock trigger lowers stock and raises low-stock alerts exactly as in real use.
    /// </summary>
    private async Task SeedSalesHistoryAsync(Product[] products, CancellationToken ct)
    {
        var random = new Random(42); // fixed seed: the same demo data on every start
        var remaining = products.ToDictionary(p => p.Id, p => p.QuantityInStock);
        var customers = new[] { null, "Ravi Patel", null, "Meera Shah", null, "Kiran Traders" };
        var today = DateTime.UtcNow.Date;

        for (var daysAgo = 13; daysAgo >= 0; daysAgo--)
        {
            var salesToday = random.Next(3, 7);
            for (var i = 0; i < salesToday; i++)
            {
                var items = new List<(Product, int)>();
                foreach (var product in products.OrderBy(_ => random.Next()).Take(random.Next(1, 4)))
                {
                    var wanted = random.Next(1, 5);
                    // keep a few units back so the demo is not sold out, but let some products run low
                    var cap = remaining[product.Id] - (product.Sku == "TEA-500" ? 6 : 3);
                    var quantity = Math.Min(wanted, cap);
                    if (quantity <= 0) continue;
                    remaining[product.Id] -= quantity;
                    items.Add((product, quantity));
                }
                if (items.Count == 0) continue;

                var soldAt = today.AddDays(-daysAgo).AddHours(4 + i * 2 + random.Next(0, 2)).AddMinutes(random.Next(0, 59));
                if (soldAt > DateTime.UtcNow) soldAt = DateTime.UtcNow.AddMinutes(-1);
                db.Sales.Add(Sale.Create(customers[random.Next(customers.Length)], "demo", soldAt, items));
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
