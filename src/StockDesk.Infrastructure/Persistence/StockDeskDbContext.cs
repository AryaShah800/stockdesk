using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StockDesk.Domain.Entities;

namespace StockDesk.Infrastructure.Persistence;

public sealed class StockDeskDbContext(DbContextOptions<StockDeskDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<LowStockAlert> LowStockAlerts => Set<LowStockAlert>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(StockDeskDbContext).Assembly);
    }
}
