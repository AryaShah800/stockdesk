using FluentAssertions;
using Moq;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Sales;
using StockDesk.Domain.Entities;

namespace StockDesk.UnitTests.Services;

public sealed class SaleServiceTests
{
    private readonly UnitOfWorkMock _uow = new();
    private readonly Mock<TimeProvider> _clock = new();
    private readonly SaleService _service;

    public SaleServiceTests()
    {
        _clock.Setup(c => c.GetUtcNow()).Returns(new DateTimeOffset(TestData.Now));
        _service = new SaleService(_uow.Object, _clock.Object);
    }

    private void GivenProducts(params Product[] products) =>
        _uow.Products.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) => products.Where(p => ids.Contains(p.Id)).ToList());

    [Fact]
    public async Task Creates_the_sale_with_the_clock_time_and_the_seller_and_saves_once()
    {
        GivenProducts(TestData.Product(1, unitPrice: 10m), TestData.Product(2, "NB", unitPrice: 50m));
        Sale? added = null;
        _uow.Sales.Setup(r => r.Add(It.IsAny<Sale>())).Callback<Sale>(s => { added = s.WithId(17); });

        var result = await _service.CreateAsync(
            new CreateSaleInput("Ravi", [new SaleItemInput(1, 2), new SaleItemInput(2, 1)]), "staff@shop.test");

        result.Succeeded.Should().BeTrue();
        result.Value.Should().Be(17);
        added!.SoldAtUtc.Should().Be(TestData.Now);
        added.SoldBy.Should().Be("staff@shop.test");
        added.Total.Should().Be(70m);
        _uow.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Looks_up_each_product_once_even_when_listed_twice()
    {
        GivenProducts(TestData.Product(1));

        await _service.CreateAsync(new CreateSaleInput(null, [new SaleItemInput(1, 1), new SaleItemInput(1, 2)]), "staff");

        _uow.Products.Verify(r => r.GetByIdsAsync(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(1)), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Empty_sale_is_rejected_without_touching_the_database()
    {
        var result = await _service.CreateAsync(new CreateSaleInput(null, []), "staff");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("at least one item");
        _uow.Products.VerifyNoOtherCalls();
        _uow.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Too_many_lines_are_rejected_before_any_lookup()
    {
        var items = Enumerable.Range(1, Sale.MaxLines + 1).Select(i => new SaleItemInput(i, 1)).ToList();

        var result = await _service.CreateAsync(new CreateSaleInput(null, items), "staff");

        result.Succeeded.Should().BeFalse();
        _uow.Products.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    public async Task Unknown_product_is_rejected(int productId)
    {
        GivenProducts(TestData.Product(1));

        var result = await _service.CreateAsync(
            new CreateSaleInput(null, [new SaleItemInput(1, 1), new SaleItemInput(productId, 1)]), "staff");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("One of the products was not found.");
        _uow.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Domain_rule_failure_becomes_an_error_and_nothing_is_added()
    {
        GivenProducts(TestData.Product(1, name: "Ball pen", stock: 1));

        var result = await _service.CreateAsync(new CreateSaleInput(null, [new SaleItemInput(1, 2)]), "staff");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Only 1 of Ball pen in stock.");
        _uow.Sales.Verify(r => r.Add(It.IsAny<Sale>()), Times.Never());
    }

    [Fact]
    public async Task Stock_taken_by_another_till_between_check_and_save_is_reported()
    {
        GivenProducts(TestData.Product(1, stock: 5));
        _uow.SaveThrows(new InsufficientStockException("Not enough stock for one or more items."));

        var result = await _service.CreateAsync(new CreateSaleInput(null, [new SaleItemInput(1, 5)]), "staff");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().StartWith("Not enough stock");
    }

    [Fact]
    public async Task Unexpected_database_errors_are_not_swallowed()
    {
        GivenProducts(TestData.Product(1));
        _uow.SaveThrows(new TimeoutException());

        var act = () => _service.CreateAsync(new CreateSaleInput(null, [new SaleItemInput(1, 1)]), "staff");

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Cancellation_token_reaches_the_repository_and_the_save()
    {
        using var cts = new CancellationTokenSource();
        GivenProducts(TestData.Product(1));

        await _service.CreateAsync(new CreateSaleInput(null, [new SaleItemInput(1, 1)]), "staff", cts.Token);

        _uow.Products.Verify(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), cts.Token));
        _uow.Uow.Verify(u => u.SaveChangesAsync(cts.Token));
    }
}
