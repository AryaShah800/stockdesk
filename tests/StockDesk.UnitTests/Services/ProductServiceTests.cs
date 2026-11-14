using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Products;
using StockDesk.Domain.Entities;

namespace StockDesk.UnitTests.Services;

public sealed class ProductServiceTests
{
    private readonly UnitOfWorkMock _uow = new();
    private readonly ProductService _service;

    private static readonly ProductInput ValidInput = new("pen-01", "Ball pen", 1, null, 6m, 10m, 50, 10);

    public ProductServiceTests()
    {
        _service = new ProductService(_uow.Object);
        CategoryExists(true);
        SupplierExists(true);
    }

    private void CategoryExists(bool exists) =>
        _uow.Categories.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(exists);

    private void SupplierExists(bool exists) =>
        _uow.Suppliers.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Supplier, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(exists);

    [Fact]
    public async Task Create_adds_the_product_with_a_normalised_sku_and_returns_its_id()
    {
        Product? added = null;
        _uow.Products.Setup(r => r.Add(It.IsAny<Product>())).Callback<Product>(p => added = p.WithId(5));

        var result = await _service.CreateAsync(ValidInput);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().Be(5);
        added!.Sku.Should().Be("PEN-01");
        _uow.Products.Verify(r => r.SkuExistsAsync("PEN-01", null, It.IsAny<CancellationToken>()));
        _uow.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_sku()
    {
        _uow.Products.Setup(r => r.SkuExistsAsync("PEN-01", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.CreateAsync(ValidInput);

        result.Error.Should().Be("SKU PEN-01 is already used.");
        _uow.Products.Verify(r => r.Add(It.IsAny<Product>()), Times.Never());
    }

    [Fact]
    public async Task Create_reports_a_duplicate_sku_that_slipped_past_the_check()
    {
        _uow.SaveThrows(new DuplicateValueException("That value is already used by another record."));

        var result = await _service.CreateAsync(ValidInput);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("already used");
    }

    [Fact]
    public async Task Create_rejects_an_unknown_category_or_supplier()
    {
        CategoryExists(false);
        (await _service.CreateAsync(ValidInput)).Error.Should().Be("Category not found.");

        CategoryExists(true);
        SupplierExists(false);
        (await _service.CreateAsync(ValidInput with { SupplierId = 9 })).Error.Should().Be("Supplier not found.");

        _uow.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Create_with_no_supplier_does_not_check_suppliers()
    {
        SupplierExists(false);

        var result = await _service.CreateAsync(ValidInput with { SupplierId = null });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Create_turns_rule_violations_into_errors()
    {
        var result = await _service.CreateAsync(ValidInput with { UnitPrice = 0m });

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task Update_of_a_missing_product_fails()
    {
        var result = await _service.UpdateAsync(3, ValidInput);

        result.Error.Should().Be("Product not found.");
    }

    [Fact]
    public async Task Update_changes_the_tracked_product_and_saves()
    {
        var product = TestData.Product(3);
        _uow.Products.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _service.UpdateAsync(3, ValidInput with { Name = "Gel pen", UnitPrice = 12m });

        result.Succeeded.Should().BeTrue();
        product.Name.Should().Be("Gel pen");
        product.UnitPrice.Should().Be(12m);
        _uow.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Update_reports_a_concurrent_edit()
    {
        _uow.Products.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(TestData.Product(3));
        _uow.SaveThrows(new ConcurrencyException("Someone else changed this record at the same time."));

        var result = await _service.UpdateAsync(3, ValidInput);

        result.Error.Should().StartWith("Someone else changed");
    }

    [Fact]
    public async Task Restock_adds_stock_and_saves()
    {
        var product = TestData.Product(3, stock: 2);
        _uow.Products.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _service.RestockAsync(3, 48);

        result.Succeeded.Should().BeTrue();
        product.QuantityInStock.Should().Be(50);
        _uow.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Restock_with_a_bad_quantity_does_not_save()
    {
        _uow.Products.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(TestData.Product(3));

        var result = await _service.RestockAsync(3, 0);

        result.Succeeded.Should().BeFalse();
        _uow.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetActive_toggles_and_saves(bool active)
    {
        var product = TestData.Product(3);
        if (active)
            product.Deactivate();
        _uow.Products.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _service.SetActiveAsync(3, active);

        result.Succeeded.Should().BeTrue();
        product.IsActive.Should().Be(active);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Search_with_no_term_returns_nothing_without_a_query(string? term)
    {
        var results = await _service.SearchAsync(term);

        results.Should().BeEmpty();
        _uow.Products.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Search_trims_the_term_caps_results_and_maps_fields()
    {
        var product = TestData.Product(4, "TEA-500", "Tea 500 g", 230m, stock: 3, reorderLevel: 5);
        _uow.Products.Setup(r => r.SearchAsync("tea", ProductService.MaxSearchResults, It.IsAny<CancellationToken>()))
            .ReturnsAsync([product]);

        var results = await _service.SearchAsync("  tea ");

        results.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new ProductSearchResult(4, "TEA-500", "Tea 500 g", string.Empty, 230m, 3, true));
    }

    [Fact]
    public async Task Search_cuts_an_overlong_term()
    {
        _uow.Products.Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _service.SearchAsync(new string('x', 500));

        _uow.Products.Verify(r => r.SearchAsync(It.Is<string>(t => t.Length == Product.NameMaxLength), It.IsAny<int>(), It.IsAny<CancellationToken>()));
    }
}
