using Microsoft.AspNetCore.Mvc;
using StockDesk.Application.Abstractions;
using StockDesk.Domain.Entities;

namespace StockDesk.Web.Api;

/// <summary>Read access to the stock ledger. Any signed-in user.</summary>
[ApiController]
[Route("api/v1/stock-movements")]
[Produces("application/json")]
public sealed class StockMovementsApiController(IUnitOfWork uow) : ControllerBase
{
    /// <summary>Lists stock movements, newest first.</summary>
    /// <param name="productId">Only this product.</param>
    /// <param name="type">Only this movement type (Opening, Sale, Restock, Receipt, Adjustment).</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page, 1 to 100 (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">One page of movements.</response>
    /// <response code="400">Invalid paging values or movement type.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PageDto<StockMovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PageDto<StockMovementDto>>> List(int? productId, MovementType? type, int page = 1,
        int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > ProductsApiController.MaxPageSize)
            return BadRequest(new ProblemDetails { Title = $"page must be at least 1 and pageSize between 1 and {ProductsApiController.MaxPageSize}." });

        var result = await uow.StockMovements.QueryAsync(new StockMovementQuery(productId, type, page, pageSize), ct);
        var items = result.Items.Select(m => new StockMovementDto(m.Id, m.ProductId, m.Product?.Sku ?? string.Empty, m.Type.ToString(),
            m.QuantityChange, m.QuantityAfter, m.PerformedBy, m.OccurredAtUtc, m.Reason, m.Reference, m.SaleId)).ToList();
        return PageDto<StockMovementDto>.From(new PagedResult<StockMovementDto>(items, page, pageSize, result.TotalCount));
    }
}
