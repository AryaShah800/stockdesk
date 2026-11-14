using FluentAssertions;
using StockDesk.Domain.Common;
using StockDesk.Domain.Entities;

namespace StockDesk.UnitTests.Domain;

public sealed class SaleTests
{
    [Fact]
    public void Lines_copy_the_product_name_sku_and_price_and_the_total_is_their_sum()
    {
        var pen = TestData.Product(1, "PEN-01", "Ball pen", 10m);
        var book = TestData.Product(2, "NB-A5", "Notebook", 55.50m);

        var sale = Sale.Create(" Ravi ", "staff@shop.test", TestData.Now, [(pen, 3), (book, 2)]);

        sale.CustomerName.Should().Be("Ravi");
        sale.SoldBy.Should().Be("staff@shop.test");
        sale.SoldAtUtc.Should().Be(TestData.Now);
        sale.Lines.Should().HaveCount(2);
        sale.Lines[0].Should().BeEquivalentTo(new { ProductId = 1, Sku = "PEN-01", ProductName = "Ball pen", UnitPrice = 10m, Quantity = 3, LineTotal = 30m });
        sale.Lines[1].LineTotal.Should().Be(111m);
        sale.Total.Should().Be(141m);
        sale.ItemCount.Should().Be(5);
    }

    [Fact]
    public void A_later_price_change_does_not_change_the_bill()
    {
        var pen = TestData.Product(unitPrice: 10m);
        var sale = Sale.Create(null, "staff", TestData.Now, [(pen, 2)]);

        pen.Update(pen.Name, pen.CategoryId, pen.SupplierId, pen.CostPrice, 15m, pen.ReorderLevel);

        sale.Lines[0].UnitPrice.Should().Be(10m);
        sale.Total.Should().Be(20m);
    }

    [Fact]
    public void The_same_product_twice_becomes_one_line()
    {
        var pen = TestData.Product(unitPrice: 10m);

        var sale = Sale.Create(null, "staff", TestData.Now, [(pen, 2), (pen, 3)]);

        sale.Lines.Should().ContainSingle().Which.Quantity.Should().Be(5);
        sale.Total.Should().Be(50m);
    }

    [Fact]
    public void A_blank_customer_name_means_a_walk_in_customer()
    {
        var sale = Sale.Create("   ", "staff", TestData.Now, [(TestData.Product(), 1)]);

        sale.CustomerName.Should().BeNull();
    }

    [Fact]
    public void A_sale_needs_at_least_one_item()
    {
        var act = () => Sale.Create(null, "staff", TestData.Now, []);

        act.Should().Throw<DomainException>().WithMessage("*at least one item*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantity_must_be_positive(int quantity)
    {
        var act = () => Sale.Create(null, "staff", TestData.Now, [(TestData.Product(), quantity)]);

        act.Should().Throw<DomainException>().WithMessage("*at least 1*");
    }

    [Fact]
    public void Cannot_sell_more_than_is_in_stock()
    {
        var pen = TestData.Product(name: "Ball pen", stock: 4);

        var act = () => Sale.Create(null, "staff", TestData.Now, [(pen, 5)]);

        act.Should().Throw<DomainException>().WithMessage("Only 4 of Ball pen in stock.");
    }

    [Fact]
    public void Stock_check_counts_merged_lines_together()
    {
        var pen = TestData.Product(stock: 4);

        var act = () => Sale.Create(null, "staff", TestData.Now, [(pen, 3), (pen, 2)]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Selling_exactly_the_remaining_stock_is_allowed()
    {
        var sale = Sale.Create(null, "staff", TestData.Now, [(TestData.Product(stock: 4), 4)]);

        sale.ItemCount.Should().Be(4);
    }

    [Fact]
    public void Inactive_products_cannot_be_sold()
    {
        var pen = TestData.Product(name: "Ball pen");
        pen.Deactivate();

        var act = () => Sale.Create(null, "staff", TestData.Now, [(pen, 1)]);

        act.Should().Throw<DomainException>().WithMessage("Ball pen is no longer sold.");
    }

    [Fact]
    public void Quantity_per_line_is_capped()
    {
        var pen = TestData.Product(stock: 50_000);

        var act = () => Sale.Create(null, "staff", TestData.Now, [(pen, Sale.MaxQuantityPerLine + 1)]);

        act.Should().Throw<DomainException>().WithMessage("*at most*");
    }

    [Fact]
    public void A_bill_has_at_most_100_different_products()
    {
        var items = Enumerable.Range(1, Sale.MaxLines + 1).Select(i => (TestData.Product(i, $"SKU{i}"), 1));

        var act = () => Sale.Create(null, "staff", TestData.Now, items);

        act.Should().Throw<DomainException>().WithMessage("*100*");
    }

    [Fact]
    public void Sale_time_must_be_utc()
    {
        var local = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Local);

        var act = () => Sale.Create(null, "staff", local, [(TestData.Product(), 1)]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Seller_is_required()
    {
        var act = () => Sale.Create(null, " ", TestData.Now, [(TestData.Product(), 1)]);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Customer_name_is_limited_in_length()
    {
        var act = () => Sale.Create(new string('a', 151), "staff", TestData.Now, [(TestData.Product(), 1)]);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(1, "SD-000001")]
    [InlineData(42, "SD-000042")]
    [InlineData(1234567, "SD-1234567")]
    public void Bill_number_is_the_id_padded_to_six_digits(int id, string expected)
    {
        Sale.FormatBillNumber(id).Should().Be(expected);
    }

    [Fact]
    public void Money_stays_exact_with_paise()
    {
        var item = TestData.Product(unitPrice: 0.10m, stock: 1000);

        var sale = Sale.Create(null, "staff", TestData.Now, [(item, 3)]);

        sale.Total.Should().Be(0.30m);
    }
}
