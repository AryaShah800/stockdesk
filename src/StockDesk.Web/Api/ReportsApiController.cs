using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockDesk.Application.Abstractions;

namespace StockDesk.Web.Api;

/// <summary>Sales reports. Admin only, like the report pages.</summary>
[ApiController]
[Route("api/v1/reports")]
[Produces("application/json")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class ReportsApiController(IReportService reports, ShopClock clock) : ControllerBase
{
    /// <summary>Daily sales from the usp_DailySales stored procedure. Days without sales are left out.</summary>
    /// <param name="from">First shop-local date (default: six days before the end date).</param>
    /// <param name="to">Last shop-local date (default: today).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">One row per day with sales.</response>
    /// <response code="400">End before start, or range longer than a year.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">Signed in but not an admin.</response>
    [HttpGet("daily-sales")]
    [ProducesResponseType(typeof(IReadOnlyList<DailySalesDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DailySalesDto>>> DailySales(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var end = to ?? clock.Today;
        var start = from ?? end.AddDays(-6);
        if (end < start || end.DayNumber - start.DayNumber > 366)
            return BadRequest(new ProblemDetails { Title = "Choose a range of at most one year, with the start before the end." });

        var rows = await reports.GetDailySalesAsync(start, end, ct);
        return rows.Select(r => new DailySalesDto(DateOnly.FromDateTime(r.SaleDate), r.Bills, r.ItemsSold, r.Revenue)).ToList();
    }
}
