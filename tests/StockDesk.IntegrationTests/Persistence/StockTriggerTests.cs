using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Sales;
using StockDesk.Domain.Entities;

namespace StockDesk.IntegrationTests.Persistence;

/// <summary>The trg_SaleLines_ReduceStock trigger, exercised through the real unit of work.</summary>
public sealed class StockTriggerTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc);
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task<int> SellAsync(params (Product Product, int Quantity)[] items)
    {
        await using var db = _database.CreateContext();
        var uow = _database.CreateUnitOfWork(db);
        var ids = items.Select(i => i.Product.Id).ToList();
        var fresh = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var sale = Sale.Create(null, "staff", Now, items.Select(i => (fresh[i.Product.Id], i.Quantity)));
        uow.Sales.Add(sale);
        await uow.SaveChangesAsync();
        return sale.Id;
    }

    private async Task<List<LowStockAlert>> AlertsAsync()
    {
        await using var db = _database.CreateContext();
        return await db.LowStockAlerts.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
    }

    [Fact]
    public async Task Saving_a_sale_reduces_the_stock_of_each_product()
    {
        var products = await _database.SeedProductsAsync(("A", 10m, 50, 5), ("B", 20m, 30, 5), ("C", 5m, 9, 1));

        await SellAsync((products[0], 3), (products[1], 10), (products[0], 2));

        (await _database.StockOfAsync(products[0].Id)).Should().Be(45);
        (await _database.StockOfAsync(products[1].Id)).Should().Be(20);
        (await _database.StockOfAsync(products[2].Id)).Should().Be(9);
    }

    [Fact]
    public async Task A_sale_that_would_take_stock_below_zero_is_rolled_back_entirely()
    {
        var products = await _database.SeedProductsAsync(("A", 10m, 5, 0), ("B", 10m, 5, 0));

        // Two tills load the same stock (5). The first sells 4; the second, working from stale data, tries 4 more.
        await using var tillTwo = _database.CreateContext();
        var stale = await tillTwo.Products.OrderBy(p => p.Id).ToListAsync();
        await SellAsync((products[0], 4));

        var uow = _database.CreateUnitOfWork(tillTwo);
        uow.Sales.Add(Sale.Create(null, "till2", Now, [(stale[1], 1), (stale[0], 4)]));
        var act = () => uow.SaveChangesAsync();

        (await act.Should().ThrowAsync<InsufficientStockException>()).Which.Message.Should().StartWith("Not enough stock");
        (await _database.StockOfAsync(products[0].Id)).Should().Be(1);
        (await _database.StockOfAsync(products[1].Id)).Should().Be(5, "the other line of the failed sale must not reduce stock either");
        await using var db = _database.CreateContext();
        (await db.Sales.CountAsync()).Should().Be(1);
        (await db.SaleLines.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Parallel_sales_of_the_last_units_never_oversell()
    {
        var product = (await _database.SeedProductsAsync(("HOT", 10m, 10, 0)))[0];

        var attempts = Enumerable.Range(0, 25).Select(async i =>
        {
            await using var db = _database.CreateContext();
            var service = new SaleService(_database.CreateUnitOfWork(db), TimeProvider.System);
            return await service.CreateAsync(new CreateSaleInput(null, [new SaleItemInput(product.Id, 1)]), $"till{i}");
        });
        var results = await Task.WhenAll(attempts);

        results.Count(r => r.Succeeded).Should().Be(10);
        (await _database.StockOfAsync(product.Id)).Should().Be(0);
        await using var check = _database.CreateContext();
        (await check.SaleLines.SumAsync(l => l.Quantity)).Should().Be(10);
    }

    [Fact]
    public async Task Crossing_the_reorder_level_raises_one_alert_with_the_quantity_left()
    {
        var products = await _database.SeedProductsAsync(("A", 10m, 12, 5), ("B", 10m, 100, 5));

        await SellAsync((products[0], 6), (products[1], 1));
        (await AlertsAsync()).Should().BeEmpty("6 left is still above the reorder level of 5");

        await SellAsync((products[0], 1));

        var alert = (await AlertsAsync()).Should().ContainSingle().Subject;
        alert.ProductId.Should().Be(products[0].Id);
        alert.QuantityAtAlert.Should().Be(5);
        alert.ReorderLevel.Should().Be(5);
        alert.IsOpen.Should().BeTrue();
        alert.RaisedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task No_second_alert_while_one_is_open_but_a_new_one_after_acknowledging()
    {
        var product = (await _database.SeedProductsAsync(("A", 10m, 10, 8)))[0];

        await SellAsync((product, 3));
        await SellAsync((product, 1));
        (await AlertsAsync()).Should().ContainSingle();

        await using (var db = _database.CreateContext())
        {
            var open = await db.LowStockAlerts.SingleAsync();
            open.Acknowledge("admin", Now);
            await db.SaveChangesAsync();
        }

        await SellAsync((product, 1));

        var alerts = await AlertsAsync();
        alerts.Should().HaveCount(2);
        alerts[1].IsOpen.Should().BeTrue();
        alerts[1].QuantityAtAlert.Should().Be(5);
    }

    [Fact]
    public async Task Restocking_with_a_stale_copy_after_a_sale_is_a_concurrency_conflict()
    {
        var product = (await _database.SeedProductsAsync(("A", 10m, 10, 0)))[0];
        await using var admin = _database.CreateContext();
        var stale = await admin.Products.SingleAsync(p => p.Id == product.Id);

        await SellAsync((product, 4));
        stale.Restock(10);
        var act = () => _database.CreateUnitOfWork(admin).SaveChangesAsync();

        await act.Should().ThrowAsync<ConcurrencyException>();
        (await _database.StockOfAsync(product.Id)).Should().Be(6, "the stale restock must not overwrite the sale's stock change");
    }
}
