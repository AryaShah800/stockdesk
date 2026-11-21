using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockDesk.Application.Abstractions;
using StockDesk.Domain.Entities;

namespace StockDesk.Web.Models;

public sealed class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class DashboardViewModel
{
    public int TodayBills { get; init; }
    public decimal TodayRevenue { get; init; }
    public int LowStockProducts { get; init; }
    public IReadOnlyList<LowStockAlert> OpenAlerts { get; init; } = [];
    public IReadOnlyList<Sale> RecentSales { get; init; } = [];
    public IReadOnlyList<StockDesk.Application.Reports.TrendPoint> Trend { get; init; } = [];
    public IReadOnlyList<TopProductRow> TopProducts { get; init; } = [];
    public int TrendDays { get; init; }
}

public sealed class ProductFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(Product.SkuMaxLength), RegularExpression("^[A-Za-z0-9_-]+$",
        ErrorMessage = "SKU can contain only letters, digits, hyphens and underscores.")]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(Product.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Category"), Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    public int CategoryId { get; set; }

    [Display(Name = "Supplier")]
    public int? SupplierId { get; set; }

    [Display(Name = "Cost price"), Range(0, 9_999_999_999.99)]
    public decimal CostPrice { get; set; }

    [Display(Name = "Selling price"), Range(0.01, 9_999_999_999.99)]
    public decimal UnitPrice { get; set; }

    [Display(Name = "Opening stock"), Range(0, 1_000_000)]
    public int OpeningStock { get; set; }

    [Display(Name = "Reorder level"), Range(0, 1_000_000)]
    public int ReorderLevel { get; set; }

    [BindNever, ValidateNever]
    public IEnumerable<SelectListItem> Categories { get; set; } = [];

    [BindNever, ValidateNever]
    public IEnumerable<SelectListItem> Suppliers { get; set; } = [];
}

public sealed class CategoryFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(Category.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(Category.DescriptionMaxLength)]
    public string? Description { get; set; }
}

public sealed class SupplierFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(Supplier.NameMaxLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(Supplier.PhoneMaxLength), Phone]
    public string? Phone { get; set; }

    [StringLength(Supplier.EmailMaxLength), EmailAddress]
    public string? Email { get; set; }
}

public sealed class SaleLineInputModel
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, Sale.MaxQuantityPerLine)]
    public int Quantity { get; set; }
}

public sealed class CreateSaleViewModel
{
    [Display(Name = "Customer name"), StringLength(Sale.CustomerNameMaxLength)]
    public string? CustomerName { get; set; }

    public List<SaleLineInputModel> Lines { get; set; } = [];

    /// <summary>Products already on the form, for redisplay after an error. Not posted.</summary>
    [BindNever, ValidateNever]
    public IReadOnlyDictionary<int, Product> Products { get; set; } = new Dictionary<int, Product>();
}

public sealed class BillViewModel
{
    public required Sale Sale { get; init; }
    public required DateTime SoldAtLocal { get; init; }
    public required string ShopName { get; init; }
    public string? ShopAddress { get; init; }
    public string? ShopPhone { get; init; }
}

public sealed class DailySalesViewModel
{
    [DataType(DataType.Date)]
    public DateOnly From { get; set; }

    [DataType(DataType.Date)]
    public DateOnly To { get; set; }

    public IReadOnlyList<DailySalesRow> Rows { get; set; } = [];
}

public sealed class CreateUserViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 10)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = Application.Common.Roles.Staff;
}

public sealed record UserRow(string Email, IReadOnlyList<string> Roles, bool LockedOut);

public sealed class ErrorViewModel
{
    public string? RequestId { get; set; }
}
