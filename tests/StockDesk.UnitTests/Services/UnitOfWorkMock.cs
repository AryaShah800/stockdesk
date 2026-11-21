using Moq;
using StockDesk.Application.Abstractions;
using StockDesk.Domain.Entities;

namespace StockDesk.UnitTests.Services;

/// <summary>An IUnitOfWork with a mocked repository behind each property.</summary>
internal sealed class UnitOfWorkMock
{
    public Mock<IUnitOfWork> Uow { get; } = new(MockBehavior.Strict);
    public Mock<IRepository<Category>> Categories { get; } = new();
    public Mock<IRepository<Supplier>> Suppliers { get; } = new();
    public Mock<IProductRepository> Products { get; } = new();
    public Mock<ISaleRepository> Sales { get; } = new();
    public Mock<ILowStockAlertRepository> Alerts { get; } = new();
    public Mock<IStockMovementRepository> Movements { get; } = new();

    public UnitOfWorkMock()
    {
        Uow.SetupGet(u => u.Categories).Returns(Categories.Object);
        Uow.SetupGet(u => u.Suppliers).Returns(Suppliers.Object);
        Uow.SetupGet(u => u.Products).Returns(Products.Object);
        Uow.SetupGet(u => u.Sales).Returns(Sales.Object);
        Uow.SetupGet(u => u.LowStockAlerts).Returns(Alerts.Object);
        Uow.SetupGet(u => u.StockMovements).Returns(Movements.Object);
        Uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    public IUnitOfWork Object => Uow.Object;

    public void SaveThrows(Exception ex) =>
        Uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(ex);

    public void VerifySaved(Times times) => Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
}
