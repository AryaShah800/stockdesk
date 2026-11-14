using StockDesk.Domain.Entities;

namespace StockDesk.Application.Abstractions;

/// <summary>
/// One business transaction: repositories share a single change tracker, and
/// <see cref="SaveChangesAsync"/> writes everything in one database transaction.
/// </summary>
public interface IUnitOfWork
{
    IRepository<Category> Categories { get; }
    IRepository<Supplier> Suppliers { get; }
    IProductRepository Products { get; }
    ISaleRepository Sales { get; }
    ILowStockAlertRepository LowStockAlerts { get; }

    /// <exception cref="InsufficientStockException">The stock trigger rejected a sale.</exception>
    /// <exception cref="DuplicateValueException">A unique value (such as a SKU) is already used.</exception>
    /// <exception cref="InUseException">A row is still referenced (such as a category with products).</exception>
    /// <exception cref="ConcurrencyException">Someone else changed the row first.</exception>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
