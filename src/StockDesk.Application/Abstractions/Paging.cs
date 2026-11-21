using StockDesk.Domain.Entities;

namespace StockDesk.Application.Abstractions;

/// <summary>One page of a larger result.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Filter and paging for the product list. Page is 1-based.</summary>
public sealed record ProductQuery(string? Search, int? CategoryId, bool LowStockOnly, bool IncludeInactive, int Page, int PageSize);

/// <summary>Filter and paging for the stock ledger. Page is 1-based.</summary>
public sealed record StockMovementQuery(int? ProductId, MovementType? Type, int Page, int PageSize);
