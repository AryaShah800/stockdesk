using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StockDesk.Domain.Entities;

namespace StockDesk.Infrastructure.Persistence;

internal static class UtcConverters
{
    /// <summary>SQL Server datetime2 has no kind; everything is stored as UTC, so read it back as UTC.</summary>
    public static readonly ValueConverter<DateTime, DateTime> Utc =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> NullableUtc =
        new(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        b.Property(c => c.Description).HasMaxLength(Category.DescriptionMaxLength);
        b.HasIndex(c => c.Name).IsUnique();
    }
}

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.Property(s => s.Name).HasMaxLength(Supplier.NameMaxLength).IsRequired();
        b.Property(s => s.Phone).HasMaxLength(Supplier.PhoneMaxLength);
        b.Property(s => s.Email).HasMaxLength(Supplier.EmailMaxLength);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(p => p.Sku).HasMaxLength(Product.SkuMaxLength).IsRequired();
        b.HasIndex(p => p.Sku).IsUnique();
        b.Property(p => p.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        b.HasIndex(p => p.Name);
        b.Property(p => p.CostPrice).HasPrecision(18, 2);
        b.Property(p => p.UnitPrice).HasPrecision(18, 2);
        b.Property(p => p.RowVersion).IsRowVersion();
        b.Ignore(p => p.IsLowStock);

        b.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.Property(s => s.CustomerName).HasMaxLength(Sale.CustomerNameMaxLength);
        b.Property(s => s.SoldBy).HasMaxLength(Sale.SoldByMaxLength).IsRequired();
        b.Property(s => s.SoldAtUtc).HasConversion(UtcConverters.Utc);
        b.Property(s => s.Total).HasPrecision(18, 2);
        b.HasIndex(s => s.SoldAtUtc);
        b.Ignore(s => s.BillNumber);
        b.Ignore(s => s.ItemCount);

        b.HasMany(s => s.Lines).WithOne().HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(s => s.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> b)
    {
        // EF Core must know about the trigger: it then avoids OUTPUT without INTO, which SQL Server rejects on tables with triggers.
        b.ToTable(t => t.HasTrigger(SqlObjects.ReduceStockTrigger));
        b.Property(l => l.Sku).HasMaxLength(Product.SkuMaxLength).IsRequired();
        b.Property(l => l.ProductName).HasMaxLength(Product.NameMaxLength).IsRequired();
        b.Property(l => l.UnitPrice).HasPrecision(18, 2);
        b.Property(l => l.LineTotal).HasPrecision(18, 2);
        b.HasOne<Product>().WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LowStockAlertConfiguration : IEntityTypeConfiguration<LowStockAlert>
{
    public void Configure(EntityTypeBuilder<LowStockAlert> b)
    {
        b.Property(a => a.RaisedAtUtc).HasConversion(UtcConverters.Utc);
        b.Property(a => a.AcknowledgedAtUtc).HasConversion(UtcConverters.NullableUtc);
        b.Property(a => a.AcknowledgedBy).HasMaxLength(256);
        b.Ignore(a => a.IsOpen);
        b.HasOne(a => a.Product).WithMany().HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Restrict);

        // At most one open alert per product.
        b.HasIndex(a => a.ProductId)
            .IsUnique()
            .HasFilter("[AcknowledgedAtUtc] IS NULL")
            .HasDatabaseName("UX_LowStockAlerts_ProductId_Open");
    }
}
