using StockDesk.Application.Abstractions;

namespace StockDesk.Web.Api;

/// <summary>One page of results.</summary>
public sealed record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static PageDto<T> From(PagedResult<T> r) => new(r.Items, r.Page, r.PageSize, r.TotalCount, r.TotalPages);
}

/// <summary>A product as listed.</summary>
/// <param name="Id">Product id.</param>
/// <param name="Sku">Upper-case stock-keeping unit code.</param>
/// <param name="Name">Product name.</param>
/// <param name="Category">Category name.</param>
/// <param name="UnitPrice">Selling price per unit.</param>
/// <param name="QuantityInStock">Units on hand.</param>
/// <param name="ReorderLevel">Stock at or below this is low stock.</param>
/// <param name="IsLowStock">True when quantity is at or below the reorder level.</param>
/// <param name="IsActive">False for deactivated products.</param>
public sealed record ProductDto(int Id, string Sku, string Name, string Category, decimal UnitPrice, int QuantityInStock,
    int ReorderLevel, bool IsLowStock, bool IsActive);

/// <summary>A product with cost and supplier details.</summary>
/// <param name="Product">The listed fields.</param>
/// <param name="CategoryId">Category id.</param>
/// <param name="Supplier">Supplier name, if any.</param>
/// <param name="CostPrice">What the shop paid per unit.</param>
public sealed record ProductDetailDto(ProductDto Product, int CategoryId, string? Supplier, decimal CostPrice);

/// <summary>One row of the stock ledger.</summary>
/// <param name="Id">Movement id.</param>
/// <param name="ProductId">Product id.</param>
/// <param name="Sku">Product SKU.</param>
/// <param name="Type">Opening, Sale, Restock, Receipt or Adjustment.</param>
/// <param name="QuantityChange">Positive when stock went up, negative when it went down.</param>
/// <param name="QuantityAfter">Stock right after this movement.</param>
/// <param name="PerformedBy">User who made the change.</param>
/// <param name="OccurredAtUtc">When it happened, UTC.</param>
/// <param name="Reason">Free-text reason, if any.</param>
/// <param name="Reference">Supplier delivery note, for receipts.</param>
/// <param name="SaleId">Bill id, for sales.</param>
public sealed record StockMovementDto(int Id, int ProductId, string Sku, string Type, int QuantityChange, int QuantityAfter,
    string PerformedBy, DateTime OccurredAtUtc, string? Reason, string? Reference, int? SaleId);

/// <summary>Sales totals for one shop-local day.</summary>
/// <param name="Date">Shop-local date.</param>
/// <param name="Bills">Number of bills.</param>
/// <param name="ItemsSold">Units sold.</param>
/// <param name="Revenue">Total revenue.</param>
public sealed record DailySalesDto(DateOnly Date, int Bills, int ItemsSold, decimal Revenue);
