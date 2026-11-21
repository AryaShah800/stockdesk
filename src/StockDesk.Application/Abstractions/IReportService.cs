namespace StockDesk.Application.Abstractions;

/// <summary>Reports served by T-SQL stored procedures.</summary>
public interface IReportService
{
    /// <summary>usp_DailySales: one row per day with at least one sale, for dates in [from, to].</summary>
    Task<IReadOnlyList<DailySalesRow>> GetDailySalesAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>usp_StockValuation: active stock per category at cost and at selling price.</summary>
    Task<IReadOnlyList<StockValuationRow>> GetStockValuationAsync(CancellationToken ct = default);

    /// <summary>Best sellers by revenue for sales on shop-local dates in [from, to] (one query over the bill lines).</summary>
    Task<IReadOnlyList<TopProductRow>> GetTopProductsAsync(DateOnly from, DateOnly to, int take, CancellationToken ct = default);
}

public sealed record DailySalesRow(DateTime SaleDate, int Bills, int ItemsSold, decimal Revenue);

public sealed record StockValuationRow(string Category, int Products, int Units, decimal CostValue, decimal RetailValue);

public sealed record TopProductRow(string Sku, string ProductName, int Units, decimal Revenue);
