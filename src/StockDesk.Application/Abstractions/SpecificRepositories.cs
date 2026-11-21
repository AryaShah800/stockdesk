using StockDesk.Domain.Entities;

namespace StockDesk.Application.Abstractions;

public interface IProductRepository : IRepository<Product>
{
    /// <summary>Products with their category and supplier, ordered by name.</summary>
    Task<IReadOnlyList<Product>> ListWithDetailsAsync(bool includeInactive, CancellationToken ct = default);

    Task<Product?> GetWithDetailsAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    /// <summary>Active products whose name or SKU contains the term, best matches first.</summary>
    Task<IReadOnlyList<Product>> SearchAsync(string term, int take, CancellationToken ct = default);

    Task<bool> SkuExistsAsync(string sku, int? exceptProductId, CancellationToken ct = default);

    /// <summary>Filtered, paged list with category and supplier, ordered by name.</summary>
    Task<PagedResult<Product>> QueryAsync(ProductQuery query, CancellationToken ct = default);

    Task<int> CountLowStockAsync(CancellationToken ct = default);
}

public interface ISaleRepository : IRepository<Sale>
{
    Task<Sale?> GetWithLinesAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Sale>> ListRecentAsync(int take, CancellationToken ct = default);

    Task<(int Bills, decimal Revenue)> TotalsBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public interface ILowStockAlertRepository : IRepository<LowStockAlert>
{
    /// <summary>Unacknowledged alerts with their product, oldest first.</summary>
    Task<IReadOnlyList<LowStockAlert>> ListOpenAsync(CancellationToken ct = default);
}

public interface IStockMovementRepository
{
    void Add(StockMovement movement);

    /// <summary>Filtered, paged ledger, newest first. Includes the product.</summary>
    Task<PagedResult<StockMovement>> QueryAsync(StockMovementQuery query, CancellationToken ct = default);

    /// <summary>Newest first. Includes the product.</summary>
    Task<IReadOnlyList<StockMovement>> ListForProductAsync(int productId, int take, CancellationToken ct = default);

    /// <summary>Newest first across all products. Includes the product.</summary>
    Task<IReadOnlyList<StockMovement>> ListRecentAsync(int take, CancellationToken ct = default);
}
