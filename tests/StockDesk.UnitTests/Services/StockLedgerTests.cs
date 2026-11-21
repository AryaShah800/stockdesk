using FluentAssertions;
using Moq;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Products;
using StockDesk.Domain.Common;
using StockDesk.Domain.Entities;

namespace StockDesk.UnitTests.Services;

public sealed class StockMovementTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_movement_records_the_change_the_balance_and_who_did_it()
    {
        var product = TestData.Product(stock: 20);

        var movement = new StockMovement(product, MovementType.Restock, 5, 25, " admin@x.test ", Now, " Weekly top-up ");

        movement.QuantityChange.Should().Be(5);
        movement.QuantityAfter.Should().Be(25);
        movement.PerformedBy.Should().Be("admin@x.test");
        movement.Reason.Should().Be("Weekly top-up");
        movement.Product.Should().BeSameAs(product);
    }

    [Fact]
    public void A_zero_change_a_negative_balance_a_local_time_and_a_sale_type_are_rejected()
    {
        var product = TestData.Product();

        new Func<object>(() => new StockMovement(product, MovementType.Restock, 0, 10, "a", Now)).Should().Throw<DomainException>();
        new Func<object>(() => new StockMovement(product, MovementType.Adjustment, -5, -1, "a", Now)).Should().Throw<DomainException>();
        new Func<object>(() => new StockMovement(product, MovementType.Sale, -1, 9, "a", Now)).Should().Throw<DomainException>()
            .WithMessage("*trigger*");
        new Func<object>(() => new StockMovement(product, MovementType.Restock, 1, 11, "a", DateTime.SpecifyKind(Now, DateTimeKind.Local)))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Adjust_changes_stock_in_both_directions_but_never_below_zero()
    {
        var product = TestData.Product(stock: 10);

        product.Adjust(-4);
        product.QuantityInStock.Should().Be(6);
        product.Adjust(3);
        product.QuantityInStock.Should().Be(9);

        new Action(() => product.Adjust(-10)).Should().Throw<DomainException>().WithMessage("*cannot be reduced*");
        new Action(() => product.Adjust(0)).Should().Throw<DomainException>();
        product.QuantityInStock.Should().Be(9);
    }
}

public sealed class StockLedgerServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 6, 0, 0, DateTimeKind.Utc);

    private readonly UnitOfWorkMock _uow = new();
    private readonly List<StockMovement> _added = [];
    private readonly StockLedgerService _service;

    public StockLedgerServiceTests()
    {
        _uow.Movements.Setup(m => m.Add(It.IsAny<StockMovement>())).Callback<StockMovement>(_added.Add);
        _service = new StockLedgerService(_uow.Object, new FixedClock(Now));
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    private Product ProductWithStock(int id, int stock)
    {
        var product = TestData.Product(id, sku: $"P-{id}", stock: stock);
        _uow.Products.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        return product;
    }

    [Fact]
    public async Task Adjust_changes_stock_and_writes_one_ledger_row_with_the_balance_after()
    {
        var product = ProductWithStock(1, 10);

        var result = await _service.AdjustAsync(1, -3, "Damaged in transit", "admin@x.test");

        result.Succeeded.Should().BeTrue();
        product.QuantityInStock.Should().Be(7);
        var row = _added.Should().ContainSingle().Subject;
        (row.Type, row.QuantityChange, row.QuantityAfter, row.Reason, row.PerformedBy, row.OccurredAtUtc)
            .Should().Be((MovementType.Adjustment, -3, 7, "Damaged in transit", "admin@x.test", Now));
        _uow.VerifySaved(Times.Once());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Adjust_needs_a_reason(string? reason)
    {
        ProductWithStock(1, 10);

        var result = await _service.AdjustAsync(1, -1, reason, "admin");

        result.Error.Should().Contain("reason");
        _added.Should().BeEmpty();
        _uow.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Adjust_below_zero_fails_without_a_ledger_row_or_a_save()
    {
        ProductWithStock(1, 2);

        var result = await _service.AdjustAsync(1, -5, "Miscount", "admin");

        result.Succeeded.Should().BeFalse();
        _added.Should().BeEmpty();
        _uow.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Receive_adds_stock_per_product_with_supplier_and_reference_and_merges_repeated_lines()
    {
        var a = ProductWithStock(1, 10);
        var b = ProductWithStock(2, 0);
        _uow.Suppliers.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new Supplier("Shree Traders").WithId(7));
        _uow.Products.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync([a, b]);

        var result = await _service.ReceiveAsync(new ReceiveStockInput(7, "DN-114", [new(1, 5), new(2, 20), new(1, 5)]), "admin");

        result.Succeeded.Should().BeTrue();
        result.Value.Should().Be(2);
        a.QuantityInStock.Should().Be(20);
        b.QuantityInStock.Should().Be(20);
        _added.Should().HaveCount(2).And.OnlyContain(m => m.Type == MovementType.Receipt && m.SupplierId == 7 && m.Reference == "DN-114");
        _added.Single(m => m.ProductId == 1).QuantityChange.Should().Be(10);
        _uow.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Receive_rejects_empty_unknown_and_non_positive_input_without_saving()
    {
        var a = ProductWithStock(1, 10);
        _uow.Suppliers.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new Supplier("S").WithId(7));
        _uow.Suppliers.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync((Supplier?)null);
        _uow.Products.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync([a]);

        (await _service.ReceiveAsync(new ReceiveStockInput(7, null, []), "admin")).Succeeded.Should().BeFalse();
        (await _service.ReceiveAsync(new ReceiveStockInput(7, null, [new(1, -2)]), "admin")).Error.Should().Contain("at least 1");
        (await _service.ReceiveAsync(new ReceiveStockInput(9, null, [new(1, 2)]), "admin")).Error.Should().Contain("Supplier not found");
        (await _service.ReceiveAsync(new ReceiveStockInput(7, null, [new(1, 2), new(99, 2)]), "admin")).Error.Should().Contain("not found");

        a.QuantityInStock.Should().Be(10);
        _added.Should().BeEmpty();
        _uow.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task ProductService_restock_and_create_also_write_the_ledger()
    {
        var product = ProductWithStock(3, 2);
        var service = new ProductService(_uow.Object, new FixedClock(Now));

        (await service.RestockAsync(3, 48, default, "admin")).Succeeded.Should().BeTrue();

        product.QuantityInStock.Should().Be(50);
        var row = _added.Should().ContainSingle().Subject;
        (row.Type, row.QuantityChange, row.QuantityAfter, row.PerformedBy).Should().Be((MovementType.Restock, 48, 50, "admin"));
    }
}
