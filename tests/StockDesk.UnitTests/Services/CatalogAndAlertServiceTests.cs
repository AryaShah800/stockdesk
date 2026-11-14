using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Alerts;
using StockDesk.Application.Catalog;
using StockDesk.Domain.Entities;

namespace StockDesk.UnitTests.Services;

public sealed class CatalogAndAlertServiceTests
{
    private readonly UnitOfWorkMock _uow = new();

    private void ProductsMatch(bool any) =>
        _uow.Products.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(any);

    private void CategoryNameTaken(bool taken) =>
        _uow.Categories.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Category, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(taken);

    [Fact]
    public async Task Create_category_adds_and_saves()
    {
        CategoryNameTaken(false);

        var result = await new CatalogService(_uow.Object).CreateCategoryAsync("Grocery", null);

        result.Succeeded.Should().BeTrue();
        _uow.Categories.Verify(r => r.Add(It.Is<Category>(c => c.Name == "Grocery")));
        _uow.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Duplicate_category_name_is_rejected()
    {
        CategoryNameTaken(true);

        var result = await new CatalogService(_uow.Object).CreateCategoryAsync(" Grocery ", null);

        result.Error.Should().Be("A category named Grocery already exists.");
        _uow.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Category_with_products_cannot_be_deleted()
    {
        _uow.Categories.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Category("Grocery").WithId(1));
        ProductsMatch(true);

        var result = await new CatalogService(_uow.Object).DeleteCategoryAsync(1);

        result.Succeeded.Should().BeFalse();
        _uow.Categories.Verify(r => r.Remove(It.IsAny<Category>()), Times.Never());
    }

    [Fact]
    public async Task Empty_category_is_deleted()
    {
        var category = new Category("Grocery").WithId(1);
        _uow.Categories.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        ProductsMatch(false);

        var result = await new CatalogService(_uow.Object).DeleteCategoryAsync(1);

        result.Succeeded.Should().BeTrue();
        _uow.Categories.Verify(r => r.Remove(category));
        _uow.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Delete_reports_a_reference_added_after_the_check()
    {
        _uow.Categories.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Category("Grocery").WithId(1));
        ProductsMatch(false);
        _uow.SaveThrows(new InUseException("This record is still in use by other records."));

        var result = await new CatalogService(_uow.Object).DeleteCategoryAsync(1);

        result.Error.Should().Contain("still in use");
    }

    [Fact]
    public async Task Supplier_used_by_products_cannot_be_deleted()
    {
        _uow.Suppliers.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new Supplier("S").WithId(2));
        ProductsMatch(true);

        var result = await new CatalogService(_uow.Object).DeleteSupplierAsync(2);

        result.Succeeded.Should().BeFalse();
        _uow.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Invalid_supplier_email_is_an_error()
    {
        var result = await new CatalogService(_uow.Object).CreateSupplierAsync("S", null, "not-an-email");

        result.Error.Should().Be("Email is not valid.");
    }

    [Fact]
    public async Task Missing_records_are_reported()
    {
        var catalog = new CatalogService(_uow.Object);

        (await catalog.UpdateCategoryAsync(9, "X", null)).Error.Should().Be("Category not found.");
        (await catalog.UpdateSupplierAsync(9, "X", null, null)).Error.Should().Be("Supplier not found.");
        (await catalog.DeleteSupplierAsync(9)).Error.Should().Be("Supplier not found.");
    }

    [Fact]
    public async Task Acknowledge_uses_the_clock_and_saves()
    {
        var alert = TestData.OpenAlert(4);
        _uow.Alerts.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(alert);
        var clock = new Mock<TimeProvider>();
        clock.Setup(c => c.GetUtcNow()).Returns(new DateTimeOffset(TestData.Now));

        var result = await new AlertService(_uow.Object, clock.Object).AcknowledgeAsync(4, "admin@shop.test");

        result.Succeeded.Should().BeTrue();
        alert.AcknowledgedAtUtc.Should().Be(TestData.Now);
        alert.AcknowledgedBy.Should().Be("admin@shop.test");
        _uow.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Acknowledging_twice_is_an_error_not_an_exception()
    {
        var alert = TestData.OpenAlert(4);
        alert.Acknowledge("admin", TestData.Now);
        _uow.Alerts.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(alert);

        var result = await new AlertService(_uow.Object, TimeProvider.System).AcknowledgeAsync(4, "admin");

        result.Succeeded.Should().BeFalse();
        _uow.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Unknown_alert_is_reported()
    {
        var result = await new AlertService(_uow.Object, TimeProvider.System).AcknowledgeAsync(99, "admin");

        result.Error.Should().Be("Alert not found.");
    }
}
