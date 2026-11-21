using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockDesk.Application.Abstractions;
using StockDesk.Infrastructure.Persistence;

namespace StockDesk.Infrastructure.Reports;

public sealed class ShopOptions
{
    public const string Section = "Shop";

    public string Name { get; set; } = "StockDesk";
    public string? Address { get; set; }
    public string? Phone { get; set; }

    /// <summary>Shop time zone as minutes ahead of UTC. 330 is India Standard Time.</summary>
    public int UtcOffsetMinutes { get; set; } = 330;
}

internal sealed class ReportService(StockDeskDbContext db, IOptions<ShopOptions> shop) : IReportService
{
    public async Task<IReadOnlyList<DailySalesRow>> GetDailySalesAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from)
            throw new ArgumentException("The end date is before the start date.", nameof(to));

        var parameters = new[]
        {
            new SqlParameter("@From", SqlDbType.Date) { Value = from.ToDateTime(TimeOnly.MinValue) },
            new SqlParameter("@To", SqlDbType.Date) { Value = to.ToDateTime(TimeOnly.MinValue) },
            new SqlParameter("@UtcOffsetMinutes", SqlDbType.Int) { Value = shop.Value.UtcOffsetMinutes },
        };
        return await db.Database
            .SqlQueryRaw<DailySalesRow>(
                $"EXEC dbo.{SqlObjects.DailySalesProcedure} @From, @To, @UtcOffsetMinutes", parameters)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StockValuationRow>> GetStockValuationAsync(CancellationToken ct = default) =>
        await db.Database
            .SqlQueryRaw<StockValuationRow>($"EXEC dbo.{SqlObjects.StockValuationProcedure}")
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TopProductRow>> GetTopProductsAsync(DateOnly from, DateOnly to, int take, CancellationToken ct = default)
    {
        if (to < from)
            throw new ArgumentException("The end date is before the start date.", nameof(to));
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);

        // Shop-local dates to a half-open UTC range, the same rule usp_DailySales applies.
        var offset = TimeSpan.FromMinutes(shop.Value.UtcOffsetMinutes);
        var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);

        var top = await db.SaleLines
            .Where(l => db.Sales.Any(s => s.Id == l.SaleId && s.SoldAtUtc >= fromUtc && s.SoldAtUtc < toUtc))
            .GroupBy(l => new { l.ProductId, l.Sku, l.ProductName })
            .Select(g => new { g.Key.Sku, g.Key.ProductName, Units = g.Sum(l => l.Quantity), Revenue = g.Sum(l => l.LineTotal) })
            .OrderByDescending(r => r.Revenue).ThenBy(r => r.Sku)
            .Take(take)
            .ToListAsync(ct);
        return top.Select(r => new TopProductRow(r.Sku, r.ProductName, r.Units, r.Revenue)).ToList();
    }
}
