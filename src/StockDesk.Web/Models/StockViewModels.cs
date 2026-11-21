using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockDesk.Domain.Entities;

namespace StockDesk.Web.Models;

public sealed class LedgerViewModel
{
    public Product? Product { get; init; }
    public IReadOnlyList<StockMovement> Movements { get; init; } = [];
    public bool CanAdjust { get; init; }
}

public sealed class ReceiveLineModel
{
    public int ProductId { get; set; }

    [Range(0, 1_000_000)]
    public int? Quantity { get; set; }
}

public sealed class ReceiveViewModel
{
    [Required(ErrorMessage = "Choose the supplier.")]
    public int? SupplierId { get; set; }

    [StringLength(StockMovement.ReferenceMaxLength)]
    public string? Reference { get; set; }

    public List<ReceiveLineModel> Lines { get; set; } = [];

    public IEnumerable<SelectListItem> Suppliers { get; set; } = [];
    public IEnumerable<SelectListItem> Products { get; set; } = [];
}
