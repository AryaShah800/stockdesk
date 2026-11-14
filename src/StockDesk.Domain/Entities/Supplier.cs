using StockDesk.Domain.Common;

namespace StockDesk.Domain.Entities;

public sealed class Supplier : Entity
{
    public const int NameMaxLength = 150;
    public const int PhoneMaxLength = 20;
    public const int EmailMaxLength = 256;

    private Supplier() => Name = string.Empty;

    public Supplier(string name, string? phone = null, string? email = null)
    {
        Name = string.Empty;
        Update(name, phone, email);
    }

    public string Name { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }

    public void Update(string name, string? phone, string? email)
    {
        var checkedName = Guard.Required(name, "Name", NameMaxLength);
        var checkedPhone = Guard.Optional(phone, "Phone", PhoneMaxLength);
        var checkedEmail = Guard.Optional(email, "Email", EmailMaxLength);
        if (checkedEmail is not null)
        {
            var at = checkedEmail.IndexOf('@');
            if (at <= 0 || at != checkedEmail.LastIndexOf('@') || at == checkedEmail.Length - 1)
                throw new DomainException("Email is not valid.");
        }

        Name = checkedName;
        Phone = checkedPhone;
        Email = checkedEmail;
    }
}
