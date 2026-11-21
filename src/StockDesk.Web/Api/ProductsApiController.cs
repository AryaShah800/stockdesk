using Microsoft.AspNetCore.Mvc;
using StockDesk.Application.Abstractions;
using StockDesk.Domain.Entities;

namespace StockDesk.Web.Api;

/// <summary>Read access to the product catalogue. Any signed-in user.</summary>
[ApiController]
[Route("api/v1/products")]
[Produces("application/json")]
public sealed class ProductsApiController(IUnitOfWork uow) : ControllerBase
{
    public const int MaxPageSize = 100;

    /// <summary>Lists products, filtered and paged, ordered by name.</summary>
    /// <param name="search">Part of the name or SKU.</param>
    /// <param name="categoryId">Only this category.</param>
    /// <param name="lowStock">Only products at or below their reorder level.</param>
    /// <param name="includeInactive">Include deactivated products (default false).</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Items per page, 1 to 100 (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">One page of products.</response>
    /// <response code="400">Invalid paging values.</response>
    /// <response code="401">Not signed in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PageDto<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PageDto<ProductDto>>> List(string? search, int? categoryId, bool lowStock = false,
        bool includeInactive = false, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > MaxPageSize)
            return BadRequest(new ProblemDetails { Title = $"page must be at least 1 and pageSize between 1 and {MaxPageSize}." });

        var result = await uow.Products.QueryAsync(new ProductQuery(search, categoryId, lowStock, includeInactive, page, pageSize), ct);
        return PageDto<ProductDto>.From(new PagedResult<ProductDto>(result.Items.Select(ToDto).ToList(), page, pageSize, result.TotalCount));
    }

    /// <summary>Gets one product with its cost price and supplier.</summary>
    /// <param name="id">Product id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The product.</response>
    /// <response code="404">No product with that id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailDto>> Get(int id, CancellationToken ct)
    {
        var p = await uow.Products.GetWithDetailsAsync(id, ct);
        return p is null ? NotFound() : new ProductDetailDto(ToDto(p), p.CategoryId, p.Supplier?.Name, p.CostPrice);
    }

    internal static ProductDto ToDto(Product p) => new(p.Id, p.Sku, p.Name, p.Category?.Name ?? string.Empty, p.UnitPrice,
        p.QuantityInStock, p.ReorderLevel, p.IsLowStock, p.IsActive);
}
