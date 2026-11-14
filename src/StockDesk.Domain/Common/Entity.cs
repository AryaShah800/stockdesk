namespace StockDesk.Domain.Common;

/// <summary>Base type for entities with a database-generated integer key.</summary>
public abstract class Entity
{
    public int Id { get; protected set; }
}
