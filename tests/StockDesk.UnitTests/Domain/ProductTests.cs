using FluentAssertions;
using StockDesk.Domain.Common;
using StockDesk.Domain.Entities;

namespace StockDesk.UnitTests.Domain;

public sealed class ProductTests
{
    [Fact]
    public void New_product_is_active_with_opening_stock_and_an_upper_case_sku()
    {
        var product = new Product("  pen-blu_01 ", "  Ball pen ", 3, 7, 6.50m, 10m, 40, 5);

        product.Sku.Should().Be("PEN-BLU_01");
        product.Name.Should().Be("Ball pen");
        product.CategoryId.Should().Be(3);
        product.SupplierId.Should().Be(7);
        product.QuantityInStock.Should().Be(40);
        product.IsActive.Should().BeTrue();
        product.IsLowStock.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PEN 01")]
    [InlineData("PEN/01")]
    [InlineData("PÉN")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567")]
    public void Sku_must_be_short_and_use_only_letters_digits_hyphens_and_underscores(string sku)
    {
        var act = () => new Product(sku, "Pen", 1, null, 1m, 2m, 0, 0);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(-0.01, 1)]
    [InlineData(1.001, 2)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    [InlineData(1, 2.555)]
    public void Prices_must_be_valid_money_and_the_selling_price_positive(decimal cost, decimal price)
    {
        var act = () => new Product("SKU", "Pen", 1, null, cost, price, 0, 0);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cost_price_of_zero_is_allowed_for_free_stock()
    {
        var product = new Product("FREE", "Sample", 1, null, 0m, 1m, 0, 0);

        product.CostPrice.Should().Be(0m);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(-1, null)]
    [InlineData(1, 0)]
    [InlineData(1, -2)]
    public void Category_is_required_and_supplier_must_be_a_real_id_when_given(int categoryId, int? supplierId)
    {
        var act = () => new Product("SKU", "Pen", categoryId, supplierId, 1m, 2m, 0, 0);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Opening_stock_and_reorder_level_cannot_be_negative()
    {
        FluentActions.Invoking(() => new Product("SKU", "Pen", 1, null, 1m, 2m, -1, 0)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => new Product("SKU", "Pen", 1, null, 1m, 2m, 0, -1)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Name_is_required_and_limited_in_length()
    {
        FluentActions.Invoking(() => new Product("SKU", " ", 1, null, 1m, 2m, 0, 0)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => new Product("SKU", new string('x', 201), 1, null, 1m, 2m, 0, 0))
            .Should().Throw<DomainException>().WithMessage("*200*");
    }

    [Fact]
    public void Restock_adds_to_stock()
    {
        var product = TestData.Product(stock: 5);

        product.Restock(20);

        product.QuantityInStock.Should().Be(25);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1_000_001)]
    public void Restock_quantity_must_be_between_1_and_a_million(int quantity)
    {
        var product = TestData.Product(stock: 5);

        product.Invoking(p => p.Restock(quantity)).Should().Throw<DomainException>();
        product.QuantityInStock.Should().Be(5);
    }

    [Fact]
    public void Restock_cannot_overflow_the_stock_counter()
    {
        var product = TestData.Product(stock: int.MaxValue - 10);

        product.Invoking(p => p.Restock(11)).Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(10, 10, true)]
    [InlineData(9, 10, true)]
    [InlineData(11, 10, false)]
    [InlineData(0, 0, true)]
    public void Low_stock_means_at_or_below_the_reorder_level(int stock, int reorderLevel, bool expected)
    {
        TestData.Product(stock: stock, reorderLevel: reorderLevel).IsLowStock.Should().Be(expected);
    }

    [Fact]
    public void Update_changes_details_but_not_sku_or_stock()
    {
        var product = TestData.Product(sku: "PEN-01", stock: 30);

        product.Update("Gel pen", 2, 4, 7m, 12.50m, 8);

        product.Name.Should().Be("Gel pen");
        product.CategoryId.Should().Be(2);
        product.SupplierId.Should().Be(4);
        product.CostPrice.Should().Be(7m);
        product.UnitPrice.Should().Be(12.50m);
        product.ReorderLevel.Should().Be(8);
        product.Sku.Should().Be("PEN-01");
        product.QuantityInStock.Should().Be(30);
    }

    [Fact]
    public void A_failed_update_leaves_the_product_unchanged()
    {
        var product = TestData.Product(name: "Ball pen", unitPrice: 10m);

        product.Invoking(p => p.Update("Gel pen", 1, null, 1m, 0m, 1)).Should().Throw<DomainException>();

        product.Name.Should().Be("Ball pen");
        product.UnitPrice.Should().Be(10m);
    }

    [Fact]
    public void Deactivate_and_activate_toggle_the_flag()
    {
        var product = TestData.Product();

        product.Deactivate();
        product.IsActive.Should().BeFalse();
        product.Activate();
        product.IsActive.Should().BeTrue();
    }
}
