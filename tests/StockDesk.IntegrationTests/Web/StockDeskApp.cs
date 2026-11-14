using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StockDesk.Application.Common;
using StockDesk.IntegrationTests.Persistence;

namespace StockDesk.IntegrationTests.Web;

/// <summary>Hosts the real MVC app against a throwaway LocalDB database.</summary>
public sealed partial class StockDeskApp : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@stockdesk.test";
    public const string StaffEmail = "staff@stockdesk.test";
    public const string Password = "Test-Passw0rd-123";

    public StockDeskApp() => Database = new TestDatabase();

    public TestDatabase Database { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:StockDesk", Database.ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", Password);
        builder.UseSetting("Seed:DemoData", "false");
    }

    public async Task EnsureStaffUserAsync()
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        if (await users.FindByEmailAsync(StaffEmail) is not null)
            return;
        var staff = new IdentityUser { UserName = StaffEmail, Email = StaffEmail, EmailConfirmed = true };
        (await users.CreateAsync(staff, Password)).Succeeded.Should().BeTrue();
        (await users.AddToRoleAsync(staff, Roles.Staff)).Succeeded.Should().BeTrue();
    }

    public HttpClient CreateBrowser() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    /// <summary>Logs in through the real login form, so the client carries the auth cookie.</summary>
    public async Task<HttpClient> LoginAsync(string email)
    {
        if (email == StaffEmail)
            await EnsureStaffUserAsync();
        var client = CreateBrowser();
        var response = await PostFormAsync(client, "/Account/Login", "/Account/Login", new()
        {
            ["Email"] = email,
            ["Password"] = Password,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, "login should succeed");
        return client;
    }

    /// <summary>Loads <paramref name="formPage"/> for its antiforgery token, then posts the form.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string formPage, string action,
        Dictionary<string, string> fields)
    {
        var page = await client.GetStringAsync(formPage);
        var token = TokenPattern().Match(page);
        token.Success.Should().BeTrue($"{formPage} should contain an antiforgery token");
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        return await client.PostAsync(action, new FormUrlEncodedContent(fields));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            Database.Dispose();
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();
}
