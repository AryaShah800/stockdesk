using StockDesk.Domain.Common;

namespace StockDesk.Domain.Entities;

/// <summary>A bill. Saving it reduces stock through a database trigger on its lines.</summary>
public sealed class Sale : Entity
{
    public const int CustomerNameMaxLength = 150;
    public const int SoldByMaxLength = 256;
    public const int MaxQuantityPerLine = 10_000;
    public const int MaxLines = 100;

    private readonly List<SaleLine> _lines = [];

    private Sale() => SoldBy = string.Empty;

    private Sale(string? customerName, string soldBy, DateTime soldAtUtc)
    {
        CustomerName = Guard.Optional(customerName, "Customer name", CustomerNameMaxLength);
        SoldBy = Guard.Required(soldBy, "Sold by", SoldByMaxLength);
        SoldAtUtc = soldAtUtc;
    }

    public string? CustomerName { get; private set; }
    public string SoldBy { get; private set; }
    public DateTime SoldAtUtc { get; private set; }
    public decimal Total { get; private set; }
    public IReadOnlyList<SaleLine> Lines => _lines;

    public string BillNumber => FormatBillNumber(Id);
    public int ItemCount => _lines.Sum(l => l.Quantity);

    public static string FormatBillNumber(int id) => $"SD-{id:D6}";

    /// <summary>
    /// Builds a sale from products and quantities. The same product listed twice becomes one line.
    /// Stock is checked here so the user gets a clear message. The trigger checks again inside the
    /// save transaction, which is what stops two tills overselling the last unit.
    /// </summary>
    public static Sale Create(string? customerName, string soldBy, DateTime soldAtUtc,
        IEnumerable<(Product Product, int Quantity)> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (soldAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Sale time must be UTC.", nameof(soldAtUtc));

        var sale = new Sale(customerName, soldBy, soldAtUtc);
        foreach (var (product, quantity) in items)
            sale.AddLine(product, quantity);

        if (sale._lines.Count == 0)
            throw new DomainException("A sale needs at least one item.");
        return sale;
    }

    private void AddLine(Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (!product.IsActive)
            throw new DomainException($"{product.Name} is no longer sold.");
        if (quantity <= 0)
            throw new DomainException($"Quantity for {product.Name} must be at least 1.");

        var existing = _lines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existing is null && _lines.Count == MaxLines)
            throw new DomainException($"A bill can have at most {MaxLines} different products.");

        var total = (long)(existing?.Quantity ?? 0) + quantity;
        if (total > MaxQuantityPerLine)
            throw new DomainException($"Quantity for {product.Name} must be at most {MaxQuantityPerLine}.");
        if (total > product.QuantityInStock)
            throw new DomainException($"Only {product.QuantityInStock} of {product.Name} in stock.");

        if (existing is null)
            _lines.Add(new SaleLine(product, quantity));
        else
            existing.Add(quantity);

        Total = _lines.Sum(l => l.LineTotal);
    }
}
