using Microsoft.Extensions.DependencyInjection;
using StockDesk.Application.Alerts;
using StockDesk.Application.Catalog;
using StockDesk.Application.Products;
using StockDesk.Application.Sales;

namespace StockDesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProductService>();
        services.AddScoped<SaleService>();
        services.AddScoped<CatalogService>();
        services.AddScoped<AlertService>();
        return services;
    }
}
