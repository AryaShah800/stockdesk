using FluentAssertions;
using StockDesk.Domain.Common;
using StockDesk.Domain.Entities;

namespace StockDesk.UnitTests.Domain;

public sealed class CatalogEntityTests
{
    [Fact]
    public void Category_trims_name_and_treats_blank_description_as_none()
    {
        var category = new Category("  Grocery ", "  ");

        category.Name.Should().Be("Grocery");
        category.Description.Should().BeNull();
    }

    [Fact]
    public void Category_name_is_required_and_a_failed_update_changes_nothing()
    {
        FluentActions.Invoking(() => new Category("")).Should().Throw<DomainException>();

        var category = new Category("Grocery", "Food");
        category.Invoking(c => c.Update("Snacks", new string('d', 501))).Should().Throw<DomainException>();
        category.Name.Should().Be("Grocery");
        category.Description.Should().Be("Food");
    }

    [Theory]
    [InlineData("orders@example.com", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("orders", false)]
    [InlineData("@example.com", false)]
    [InlineData("orders@", false)]
    [InlineData("a@b@c", false)]
    public void Supplier_email_is_optional_but_must_look_like_an_email(string? email, bool valid)
    {
        var act = () => new Supplier("Shree Traders", null, email);

        if (valid)
            act.Should().NotThrow();
        else
            act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Supplier_update_replaces_all_fields()
    {
        var supplier = new Supplier("Old", "1", "a@b.c");

        supplier.Update("New", null, null);

        supplier.Should().BeEquivalentTo(new { Name = "New", Phone = (string?)null, Email = (string?)null });
    }

    [Fact]
    public void Supplier_phone_is_limited_to_20_characters()
    {
        FluentActions.Invoking(() => new Supplier("S", new string('9', 21))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Alert_acknowledge_records_who_and_when_and_closes_it()
    {
        var alert = TestData.OpenAlert();
        alert.IsOpen.Should().BeTrue();

        alert.Acknowledge("admin@shop.test", TestData.Now);

        alert.IsOpen.Should().BeFalse();
        alert.AcknowledgedBy.Should().Be("admin@shop.test");
        alert.AcknowledgedAtUtc.Should().Be(TestData.Now);
    }

    [Fact]
    public void Alert_cannot_be_acknowledged_twice()
    {
        var alert = TestData.OpenAlert();
        alert.Acknowledge("admin", TestData.Now);

        alert.Invoking(a => a.Acknowledge("other", TestData.Now.AddHours(1)))
            .Should().Throw<DomainException>().WithMessage("*already*");
        alert.AcknowledgedBy.Should().Be("admin");
    }
}
