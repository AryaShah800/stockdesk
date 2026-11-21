using StockDesk.Domain.Common;

namespace StockDesk.Domain.Entities;

public enum MovementType
{
    /// <summary>Stock the product started with when it was created.</summary>
    Opening = 0,

    /// <summary>A bill. Written by the sale-line trigger, in the same transaction as the sale.</summary>
    Sale = 1,

    /// <summary>Stock added by hand.</summary>
    Restock = 2,

    /// <summary>Stock received from a supplier.</summary>
    Receipt = 3,

    /// <summary>A correction after a stock count (damage, loss, miscount). Can be negative.</summary>
    Adjustment = 4,
}

/// <summary>
/// One row of the stock ledger: how a product's quantity changed, by how much, why, and who did it.
/// Rows are only ever added. <see cref="QuantityAfter"/> is the stock right after this movement, so the
/// ledger can be read as a running balance and checked against the product's current quantity.
/// </summary>
public sealed class StockMovement : Entity
{
    public const int ReasonMaxLength = 200;
    public const int ReferenceMaxLength = 60;

    private StockMovement()
    {
        PerformedBy = string.Empty;
    }

    public StockMovement(Product product, MovementType type, int quantityChange, int quantityAfter, string performedBy,
        DateTime occurredAtUtc, string? reason = null, int? supplierId = null, string? reference = null)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Movement time must be UTC.", nameof(occurredAtUtc));
        if (quantityChange == 0)
            throw new DomainException("A stock movement must change the quantity.");
        if (quantityAfter < 0)
            throw new DomainException("Stock cannot go below zero.");
        if (type == MovementType.Sale)
            throw new DomainException("Sale movements are written by the database trigger.");

        Product = product;
        ProductId = product.Id;
        Type = type;
        QuantityChange = quantityChange;
        QuantityAfter = quantityAfter;
        PerformedBy = Guard.Required(performedBy, "Performed by", Sale.SoldByMaxLength);
        OccurredAtUtc = occurredAtUtc;
        Reason = Guard.Optional(reason, "Reason", ReasonMaxLength);
        SupplierId = supplierId;
        Reference = Guard.Optional(reference, "Reference", ReferenceMaxLength);
    }

    public int ProductId { get; private set; }
    public Product? Product { get; private set; }
    public MovementType Type { get; private set; }

    /// <summary>Positive when stock went up, negative when it went down.</summary>
    public int QuantityChange { get; private set; }

    public int QuantityAfter { get; private set; }
    public string PerformedBy { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string? Reason { get; private set; }
    public int? SupplierId { get; private set; }

    /// <summary>Supplier delivery note or invoice number, for receipts.</summary>
    public string? Reference { get; private set; }

    /// <summary>The bill this movement belongs to, for sales.</summary>
    public int? SaleId { get; private set; }
}
