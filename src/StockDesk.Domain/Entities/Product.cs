using StockDesk.Domain.Common;

namespace StockDesk.Domain.Entities;

/// <summary>
/// A stock-keeping unit. Stock goes up through <see cref="Restock"/>. It goes down only when a sale is
/// saved: the trg_SaleLines_ReduceStock trigger does that in the same transaction as the sale.
/// </summary>
public sealed class Product : Entity
{
    public const int SkuMaxLength = 32;
    public const int NameMaxLength = 200;

    private Product()
    {
        Sku = string.Empty;
        Name = string.Empty;
        RowVersion = [];
    }

    public Product(string sku, string name, int categoryId, int? supplierId, decimal costPrice, decimal unitPrice,
        int openingStock, int reorderLevel)
    {
        Sku = NormaliseSku(sku);
        Name = string.Empty;
        RowVersion = [];
        SetDetails(name, categoryId, supplierId, costPrice, unitPrice, reorderLevel);
        QuantityInStock = Guard.NotNegative(openingStock, "Opening stock");
        IsActive = true;
    }

    public string Sku { get; private set; }
    public string Name { get; private set; }
    public int CategoryId { get; private set; }
    public Category? Category { get; private set; }
    public int? SupplierId { get; private set; }
    public Supplier? Supplier { get; private set; }

    /// <summary>What the shop paid per unit. Used for stock valuation.</summary>
    public decimal CostPrice { get; private set; }

    /// <summary>Selling price per unit, copied onto each sale line.</summary>
    public decimal UnitPrice { get; private set; }

    public int QuantityInStock { get; private set; }
    public int ReorderLevel { get; private set; }
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; }

    public bool IsLowStock => QuantityInStock <= ReorderLevel;

    public void Update(string name, int categoryId, int? supplierId, decimal costPrice, decimal unitPrice, int reorderLevel)
        => SetDetails(name, categoryId, supplierId, costPrice, unitPrice, reorderLevel);

    public void Restock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Restock quantity must be at least 1.");
        if (quantity > 1_000_000 || QuantityInStock > int.MaxValue - quantity)
            throw new DomainException("Restock quantity is too large.");
        QuantityInStock += quantity;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public static string NormaliseSku(string? sku)
    {
        var value = Guard.Required(sku, "SKU", SkuMaxLength).ToUpperInvariant();
        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
                throw new DomainException("SKU can contain only letters, digits, hyphens and underscores.");
        }
        return value;
    }

    private void SetDetails(string name, int categoryId, int? supplierId, decimal costPrice, decimal unitPrice, int reorderLevel)
    {
        var checkedName = Guard.Required(name, "Name", NameMaxLength);
        if (categoryId <= 0)
            throw new DomainException("Category is required.");
        if (supplierId is <= 0)
            throw new DomainException("Supplier is not valid.");
        var cost = Guard.Money(costPrice, "Cost price");
        var price = Guard.Money(unitPrice, "Unit price");
        if (price == 0)
            throw new DomainException("Unit price must be greater than zero.");
        var reorder = Guard.NotNegative(reorderLevel, "Reorder level");

        Name = checkedName;
        CategoryId = categoryId;
        SupplierId = supplierId;
        CostPrice = cost;
        UnitPrice = price;
        ReorderLevel = reorder;
    }
}
