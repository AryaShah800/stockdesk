using StockDesk.Domain.Common;

namespace StockDesk.Domain.Entities;

/// <summary>One product on a bill. Name, SKU and price are copied so an old bill never changes.</summary>
public sealed class SaleLine : Entity
{
    private SaleLine()
    {
        Sku = string.Empty;
        ProductName = string.Empty;
    }

    internal SaleLine(Product product, int quantity)
    {
        ProductId = product.Id;
        Sku = product.Sku;
        ProductName = product.Name;
        UnitPrice = product.UnitPrice;
        Quantity = quantity;
        LineTotal = UnitPrice * quantity;
    }

    public int SaleId { get; private set; }
    public int ProductId { get; private set; }
    public string Sku { get; private set; }
    public string ProductName { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotal { get; private set; }

    internal void Add(int quantity)
    {
        Quantity += quantity;
        LineTotal = UnitPrice * Quantity;
    }
}
