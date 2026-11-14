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

        db.Products.AddRange(
            new Product("PEN-BLU-01", "Ball pen, blue", stationery.Id, supplier.Id, 6.50m, 10.00m, 240, 50),
            new Product("NB-A5-200", "Notebook A5, 200 pages", stationery.Id, supplier.Id, 38.00m, 55.00m, 60, 15),
            new Product("RICE-BAS-1K", "Basmati rice 1 kg", grocery.Id, null, 95.00m, 120.00m, 40, 10),
            new Product("TEA-500", "Tea leaves 500 g", grocery.Id, null, 180.00m, 230.00m, 12, 10),
            new Product("LED-9W", "LED bulb 9 W", electrical.Id, supplier.Id, 72.00m, 99.00m, 30, 8),
            new Product("BAT-AA-4", "AA batteries, pack of 4", electrical.Id, supplier.Id, 60.00m, 85.00m, 6, 5));
        await db.SaveChangesAsync(ct);
    }
}
