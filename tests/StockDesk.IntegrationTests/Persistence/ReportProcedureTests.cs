using FluentAssertions;
using Microsoft.Extensions.Options;
using StockDesk.Application.Abstractions;
using StockDesk.Domain.Entities;
using StockDesk.Infrastructure.Reports;

namespace StockDesk.IntegrationTests.Persistence;

/// <summary>usp_DailySales and usp_StockValuation, called through the real report service.</summary>
public sealed class ReportProcedureTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private ReportService Reports(Infrastructure.Persistence.StockDeskDbContext db, int offsetMinutes = 330) =>
        new(db, Options.Create(new ShopOptions { UtcOffsetMinutes = offsetMinutes }));

    private async Task SellAtAsync(DateTime utc, params (Product Product, int Quantity)[] items)
    {
        await using var db = _database.CreateContext();
        var ids = items.Select(i => i.Product.Id).ToList();
        var fresh = db.Products.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);
        db.Sales.Add(Sale.Create(null, "staff", utc, items.Select(i => (fresh[i.Product.Id], i.Quantity))));
        await db.SaveChangesAsync();
    }

    private static DateTime Utc(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Daily_sales_groups_by_the_shop_local_date()
    {
        var products = await _database.SeedProductsAsync(("A", 10m, 500, 0), ("B", 25m, 500, 0));
        await SellAtAsync(Utc(12, 18, 29), (products[0], 1));                    // 12 Nov 23:59 IST
        await SellAtAsync(Utc(12, 18, 30), (products[0], 2), (products[1], 1));  // 13 Nov 00:00 IST
        await SellAtAsync(Utc(13, 10), (products[1], 4));                        // 13 Nov 15:30 IST
        await SellAtAsync(Utc(14, 5), (products[0], 3));                         // 14 Nov 10:30 IST

        await using var db = _database.CreateContext();
        var rows = await Reports(db).GetDailySalesAsync(new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 14));

        rows.Should().Equal(
            new DailySalesRow(new DateTime(2026, 11, 12), 1, 1, 10m),
            new DailySalesRow(new DateTime(2026, 11, 13), 2, 7, 145m),
            new DailySalesRow(new DateTime(2026, 11, 14), 1, 3, 30m));
    }

    [Fact]
    public async Task Daily_sales_includes_the_whole_end_date_and_nothing_outside_the_range()
    {
        var product = (await _database.SeedProductsAsync(("A", 10m, 500, 0)))[0];
        await SellAtAsync(Utc(9, 18, 29), (product, 1));   // 9 Nov IST: before the range
        await SellAtAsync(Utc(10, 0), (product, 1));       // 10 Nov IST
        await SellAtAsync(Utc(11, 18, 29), (product, 1));  // 11 Nov 23:59 IST: last minute of the range
        await SellAtAsync(Utc(11, 18, 30), (product, 1));  // 12 Nov IST: after the range

        await using var db = _database.CreateContext();
        var rows = await Reports(db).GetDailySalesAsync(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 11));

        rows.Select(r => r.SaleDate.Day).Should().Equal(10, 11);
        rows.Sum(r => r.Bills).Should().Be(2);
    }

    [Fact]
    public async Task Daily_sales_uses_the_configured_offset()
    {
        var product = (await _database.SeedProductsAsync(("A", 10m, 500, 0)))[0];
        await SellAtAsync(Utc(12, 20), (product, 1)); // 12 Nov in UTC, 13 Nov in IST

        await using var db = _database.CreateContext();
        (await Reports(db, 0).GetDailySalesAsync(new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 12))).Should().ContainSingle();
        (await Reports(db, 330).GetDailySalesAsync(new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 12))).Should().BeEmpty();
    }

    [Fact]
    public async Task Daily_sales_rejects_a_reversed_range()
    {
        await using var db = _database.CreateContext();

        var act = () => Reports(db).GetDailySalesAsync(new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 13));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Stock_valuation_sums_active_stock_per_category_at_cost_and_selling_price()
    {
        await using (var db = _database.CreateContext())
        {
            var stationery = new Category("Stationery");
            var grocery = new Category("Grocery");
            var empty = new Category("Empty");
            db.AddRange(stationery, grocery, empty);
            await db.SaveChangesAsync();

            var retired = new Product("OLD", "Old pen", stationery.Id, null, 1m, 2m, 1000, 0);
            retired.Deactivate();
            db.Products.AddRange(
                new Product("PEN", "Pen", stationery.Id, null, 6.50m, 10m, 100, 0),
                new Product("NB", "Notebook", stationery.Id, null, 38m, 55m, 10, 0),
                new Product("TEA", "Tea", grocery.Id, null, 180m, 230m, 3, 0),
                retired);
            await db.SaveChangesAsync();
        }

        await using var read = _database.CreateContext();
        var rows = await Reports(read).GetStockValuationAsync();

        rows.Should().Equal(
            new StockValuationRow("Grocery", 1, 3, 540m, 690m),
            new StockValuationRow("Stationery", 2, 110, 1030m, 1550m));
    }
}
