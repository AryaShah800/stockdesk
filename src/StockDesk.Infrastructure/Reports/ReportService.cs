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
}
