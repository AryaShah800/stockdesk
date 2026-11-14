using System.Reflection;
using StockDesk.Domain.Common;
using StockDesk.Domain.Entities;

namespace StockDesk.UnitTests;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 11, 14, 6, 30, 0, DateTimeKind.Utc);

    /// <summary>Sets the database-generated id, as EF Core would after saving.</summary>
    public static T WithId<T>(this T entity, int id) where T : Entity
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);
        return entity;
    }

    public static Product Product(int id = 1, string sku = "PEN-01", string name = "Ball pen", decimal unitPrice = 10m,
        int stock = 100, int reorderLevel = 10, decimal costPrice = 6m) =>
        new Product(sku, name, categoryId: 1, supplierId: null, costPrice, unitPrice, stock, reorderLevel).WithId(id);

    public static LowStockAlert OpenAlert(int id = 1)
    {
        var alert = (LowStockAlert)Activator.CreateInstance(typeof(LowStockAlert), nonPublic: true)!;
        return alert.WithId(id);
    }

    public static void Set<T>(this T entity, string property, object? value) =>
        typeof(T).GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(entity, value);
}
