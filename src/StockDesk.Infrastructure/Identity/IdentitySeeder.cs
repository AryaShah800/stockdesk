using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using StockDesk.Application.Common;

namespace StockDesk.Infrastructure.Identity;

/// <summary>
/// Creates the Admin and Staff roles and, if configured, the first Admin account.
/// The admin password is never in source control: it comes from user secrets or an environment variable.
/// </summary>
public sealed class IdentitySeeder(
    RoleManager<IdentityRole> roles,
    UserManager<IdentityUser> users,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync(string? adminEmail, string? adminPassword)
    {
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                Check(await roles.CreateAsync(new IdentityRole(role)), $"create role {role}");
        }

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            if ((await users.GetUsersInRoleAsync(Roles.Admin)).Count == 0)
                logger.LogWarning("No Admin account exists. Set Seed:AdminEmail and Seed:AdminPassword to create one.");
            return;
        }

        if (await users.FindByEmailAsync(adminEmail) is not null)
            return;

        var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        Check(await users.CreateAsync(admin, adminPassword), "create the admin account");
        Check(await users.AddToRoleAsync(admin, Roles.Admin), "add the admin account to the Admin role");
        logger.LogInformation("Created the first Admin account.");
    }

    private static void Check(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Could not {action}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
    }
}
