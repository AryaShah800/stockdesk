using StockDesk.Domain.Common;

namespace StockDesk.Domain.Entities;

public sealed class Category : Entity
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private Category() => Name = string.Empty;

    public Category(string name, string? description = null)
    {
        Name = string.Empty;
        Update(name, description);
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    public void Update(string name, string? description)
    {
        var checkedDescription = Guard.Optional(description, "Description", DescriptionMaxLength);
        Name = Guard.Required(name, "Name", NameMaxLength);
        Description = checkedDescription;
    }
}
