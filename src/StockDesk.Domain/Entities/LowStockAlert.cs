using StockDesk.Domain.Common;

namespace StockDesk.Domain.Entities;

/// <summary>
/// Raised by the database trigger when a sale leaves a product at or below its reorder level.
/// At most one open alert exists per product (a filtered unique index enforces it).
/// </summary>
public sealed class LowStockAlert : Entity
{
    private LowStockAlert() { }

    public int ProductId { get; private set; }
    public Product? Product { get; private set; }
    public int QuantityAtAlert { get; private set; }
    public int ReorderLevel { get; private set; }
    public DateTime RaisedAtUtc { get; private set; }
    public DateTime? AcknowledgedAtUtc { get; private set; }
    public string? AcknowledgedBy { get; private set; }

    public bool IsOpen => AcknowledgedAtUtc is null;

    public void Acknowledge(string by, DateTime atUtc)
    {
        if (!IsOpen)
            throw new DomainException("This alert was already acknowledged.");
        AcknowledgedBy = Guard.Required(by, "Acknowledged by", 256);
        AcknowledgedAtUtc = atUtc;
    }
}
