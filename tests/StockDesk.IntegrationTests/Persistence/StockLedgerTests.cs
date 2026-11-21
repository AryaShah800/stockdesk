using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Products;
using StockDesk.Domain.Entities;

namespace StockDesk.IntegrationTests.Persistence;

/// <summary>The stock ledger: written by the sale trigger and by the application, kept in step with the quantity.</summary>
public sealed class StockLedgerTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 11, 14, 6, 0, 0, DateTimeKind.Utc);
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task<List<StockMovement>> MovementsAsync(int productId)
    {
        await using var db = _database.CreateContext();
        return await db.StockMovements.AsNoTracking().Where(m => m.ProductId == productId).OrderBy(m => m.Id).ToListAsync();
    }

    private async Task<int> SellAsync(params (Product Product, int Quantity)[] items)
    {
        await using var db = _database.CreateContext();
        var uow = _database.CreateUnitOfWork(db);
        var ids = items.Select(i => i.Product.Id).ToList();
        var fresh = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var sale = Sale.Create("Meera", "staff@x.test", Now, items.Select(i => (fresh[i.Product.Id], i.Quantity)));
        uow.Sales.Add(sale);
        await uow.SaveChangesAsync();
        return sale.Id;
    }

    [Fact]
    public async Task A_sale_writes_one_ledger_row_per_line_with_who_when_and_the_bill()
    {
        var p = await _database.SeedProductsAsync(("A", 10m, 50, 0), ("B", 20m, 30, 0));

        var saleId = await SellAsync((p[0], 3), (p[1], 10));

        var a = (await MovementsAsync(p[0].Id)).Should().ContainSingle().Subject;
        (a.Type, a.QuantityChange, a.QuantityAfter, a.PerformedBy, a.OccurredAtUtc, a.SaleId)
            .Should().Be((MovementType.Sale, -3, 47, "staff@x.test", Now, saleId));
        a.Reason.Should().Be($"Bill {Sale.FormatBillNumber(saleId)}");
        var b = (await MovementsAsync(p[1].Id)).Should().ContainSingle().Subject;
        (b.QuantityChange, b.QuantityAfter).Should().Be((-10, 20));
    }

    [Fact]
    public async Task Consecutive_sales_build_a_running_balance_that_ends_at_the_products_quantity()
    {
        var p = (await _database.SeedProductsAsync(("A", 10m, 100, 0)))[0];

        await SellAsync((p, 10));
        await SellAsync((p, 25));
        await SellAsync((p, 5));

        var rows = await MovementsAsync(p.Id);
        rows.Select(r => r.QuantityAfter).Should().Equal(90, 65, 60);
        rows.Last().QuantityAfter.Should().Be(await _database.StockOfAsync(p.Id));
    }

    [Fact]
    public async Task A_sale_that_is_rolled_back_leaves_no_ledger_rows()
    {
        var p = await _database.SeedProductsAsync(("A", 10m, 5, 0), ("B", 10m, 5, 0));
        await using var tillTwo = _database.CreateContext();
        var stale = await tillTwo.Products.OrderBy(x => x.Id).ToListAsync();
        await SellAsync((p[0], 4));

        var uow = _database.CreateUnitOfWork(tillTwo);
        uow.Sales.Add(Sale.Create(null, "till2", Now, [(stale[1], 1), (stale[0], 4)]));
        var act = () => uow.SaveChangesAsync();

        await act.Should().ThrowAsync<InsufficientStockException>();
        (await MovementsAsync(p[0].Id)).Should().ContainSingle("only the first sale happened");
        (await MovementsAsync(p[1].Id)).Should().BeEmpty("the failed sale must not leave a row for its other line");
    }

    [Fact]
    public async Task Restock_adjustment_and_receipt_are_saved_with_the_quantity_in_one_transaction()
    {
        var p = (await _database.SeedProductsAsync(("A", 10m, 10, 0)))[0];
        int supplierId;
        await using (var db = _database.CreateContext())
        {
            var supplier = new Supplier("Shree Traders");
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            supplierId = supplier.Id;
        }

        await using (var db = _database.CreateContext())
        {
            var service = new StockLedgerService(_database.CreateUnitOfWork(db), TimeProvider.System);
            (await service.ReceiveAsync(new ReceiveStockInput(supplierId, "DN-1", [new(p.Id, 40)]), "admin")).Succeeded.Should().BeTrue();
            (await service.AdjustAsync(p.Id, -3, "Water damage", "admin")).Succeeded.Should().BeTrue();
        }
        await using (var db = _database.CreateContext())
        {
            var service = new ProductService(_database.CreateUnitOfWork(db));
            (await service.RestockAsync(p.Id, 6, default, "admin")).Succeeded.Should().BeTrue();
        }

        var rows = await MovementsAsync(p.Id);
        rows.Select(r => (r.Type, r.QuantityChange, r.QuantityAfter)).Should().Equal(
            (MovementType.Receipt, 40, 50), (MovementType.Adjustment, -3, 47), (MovementType.Restock, 6, 53));
        rows[0].SupplierId.Should().Be(supplierId);
        rows[0].Reference.Should().Be("DN-1");
        (await _database.StockOfAsync(p.Id)).Should().Be(53);
    }

    [Fact]
    public async Task Creating_a_product_with_stock_writes_an_opening_row()
    {
        int categoryId;
        await using (var db = _database.CreateContext())
        {
            var category = new Category("General");
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }

        int id;
        await using (var db = _database.CreateContext())
        {
            var result = await new ProductService(_database.CreateUnitOfWork(db))
                .CreateAsync(new ProductInput("OPEN-1", "Opening test", categoryId, null, 5m, 9m, 12, 2), default, "admin");
            result.Succeeded.Should().BeTrue();
            id = result.Value;
        }

        var row = (await MovementsAsync(id)).Should().ContainSingle().Subject;
        (row.Type, row.QuantityChange, row.QuantityAfter, row.PerformedBy).Should().Be((MovementType.Opening, 12, 12, "admin"));
    }

    [Fact]
    public async Task The_ledger_adds_up_to_the_stock_after_a_mix_of_operations()
    {
        // Start from an opening row so the ledger is complete, then mix every kind of movement.
        int categoryId;
        await using (var db = _database.CreateContext())
        {
            var category = new Category("General");
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            categoryId = category.Id;
        }
        int id;
        await using (var db = _database.CreateContext())
            id = (await new ProductService(_database.CreateUnitOfWork(db))
                .CreateAsync(new ProductInput("MIX-1", "Mix", categoryId, null, 5m, 9m, 100, 2), default, "admin")).Value;
        var product = new Product("X", "X", 1, null, 1m, 1m, 0, 0);
        await using (var db = _database.CreateContext())
            product = await db.Products.SingleAsync(x => x.Id == id);

        await SellAsync((product, 7));
        await using (var db = _database.CreateContext())
            await new StockLedgerService(_database.CreateUnitOfWork(db), TimeProvider.System).AdjustAsync(id, -2, "Count", "admin");
        await SellAsync((product, 11));
        await using (var db = _database.CreateContext())
            await new ProductService(_database.CreateUnitOfWork(db)).RestockAsync(id, 30, default, "admin");

        var rows = await MovementsAsync(id);
        rows.Sum(r => r.QuantityChange).Should().Be(await _database.StockOfAsync(id));
        rows.Last().QuantityAfter.Should().Be(await _database.StockOfAsync(id));
        rows.Select(r => r.Type).Should().Equal(MovementType.Opening, MovementType.Sale, MovementType.Adjustment, MovementType.Sale, MovementType.Restock);
    }
}
