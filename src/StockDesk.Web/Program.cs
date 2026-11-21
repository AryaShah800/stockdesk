using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockDesk.Application;
using StockDesk.Application.Common;
using StockDesk.Infrastructure;
using StockDesk.Infrastructure.Identity;
using StockDesk.Infrastructure.Persistence;
using Microsoft.OpenApi.Models;
using StockDesk.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ShopClock>();

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<StockDeskDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "StockDesk.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    // The API is called by scripts and Swagger, not browsers following redirects: answer 401/403 instead.
    options.Events.OnRedirectToLogin = ctx => ApiOr(ctx, StatusCodes.Status401Unauthorized);
    options.Events.OnRedirectToAccessDenied = ctx => ApiOr(ctx, StatusCodes.Status403Forbidden);

    static Task ApiOr(RedirectContext<CookieAuthenticationOptions> ctx, int status)
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
            ctx.Response.StatusCode = status;
        else
            ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    }
});

builder.Services.AddAuthorization(options =>
{
    // Every page needs a signed-in user unless it opts out with [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin));
});

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StockDesk API",
        Version = "v1",
        Description = "Read-only API over products, the stock ledger and sales reports. " +
                      "It uses the same cookie sign-in as the web app: sign in at /Account/Login, then call the API or Swagger UI from the same browser.",
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml"));
    options.AddSecurityDefinition("cookie", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = "StockDesk.Auth",
        Description = "Auth cookie set by /Account/Login (the browser sends it automatically).",
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var config = app.Configuration;
    var services = scope.ServiceProvider;
    if (config.GetValue<bool>("Database:MigrateOnStartup"))
        await services.GetRequiredService<StockDeskDbContext>().Database.MigrateAsync();
    if (config.GetValue("Database:SeedIdentity", true))
        await services.GetRequiredService<IdentitySeeder>().SeedAsync(config["Seed:AdminEmail"], config["Seed:AdminPassword"]);
    if (config.GetValue<bool>("Seed:DemoData"))
        await services.GetRequiredService<DemoDataSeeder>().SeedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Configuration.GetValue<bool>("Https:RedirectDisabled"))
    app.UseHttpsRedirection(); // compose serves plain HTTP on 8080
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "StockDesk API v1"));

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>Entry point, public so the integration tests can host the app.</summary>
public partial class Program;
