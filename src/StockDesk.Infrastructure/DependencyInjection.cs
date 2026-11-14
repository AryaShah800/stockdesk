using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StockDesk.Application.Abstractions;
using StockDesk.Infrastructure.Identity;
using StockDesk.Infrastructure.Persistence;
using StockDesk.Infrastructure.Reports;

namespace StockDesk.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "StockDesk";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<StockDeskDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<DemoDataSeeder>();
        services.Configure<ShopOptions>(configuration.GetSection(ShopOptions.Section));
        return services;
    }
}
