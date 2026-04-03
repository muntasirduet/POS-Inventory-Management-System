using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Identity;

namespace POS.Infrastructure.Data;

public static class DbSeeder
{
    public static readonly string[] Roles = { "SuperAdmin", "StoreOwner", "StoreManager", "Cashier", "InventoryManager", "Accountant" };

    public static async Task SeedAsync(AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        await context.Database.MigrateAsync();

        foreach (var role in Roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        const string adminEmail = "admin@pos.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, FullName = "Super Admin", EmailConfirmed = true, IsActive = true };
            var result = await userManager.CreateAsync(admin, "Admin@123456");
            if (result.Succeeded) await userManager.AddToRoleAsync(admin, "SuperAdmin");
        }

        if (!await context.TaxRates.AnyAsync())
        {
            context.TaxRates.Add(new TaxRate { Name = "Standard", Rate = 0.15m, IsDefault = true });
            await context.SaveChangesAsync();
        }

        if (!await context.Currencies.AnyAsync())
        {
            context.Currencies.Add(new Currency { Code = "USD", Symbol = "$", Locale = "en-US", IsDefault = true });
            await context.SaveChangesAsync();
        }

        if (!await context.SystemConfigs.AnyAsync())
        {
            context.SystemConfigs.AddRange(
                new SystemConfig { Key = "LoyaltyPointsPerDollar", Value = "1", Description = "Points per dollar spent" },
                new SystemConfig { Key = "LowStockThreshold", Value = "10", Description = "Default low-stock threshold" },
                new SystemConfig { Key = "StoreName", Value = "My POS Store", Description = "Store display name" },
                new SystemConfig { Key = "ReceiptFooter", Value = "Thank you for your purchase!", Description = "Receipt footer" }
            );
            await context.SaveChangesAsync();
        }

        if (!await context.Stores.AnyAsync())
        {
            var store = new Store { Name = "Main Store", OwnerId = "", Address = "123 Main St", Phone = "555-0100" };
            context.Stores.Add(store);
            await context.SaveChangesAsync();

            var branch = new Branch { StoreId = store.Id, Name = "Main Branch", Address = "123 Main St" };
            context.Branches.Add(branch);
            await context.SaveChangesAsync();

            context.Terminals.Add(new Terminal { BranchId = branch.Id, Name = "POS-1", IsActive = true });
            await context.SaveChangesAsync();
        }

        if (!await context.Categories.AnyAsync())
        {
            context.Categories.AddRange(
                new Category { Name = "General" },
                new Category { Name = "Electronics" },
                new Category { Name = "Food & Beverage" },
                new Category { Name = "Clothing" }
            );
            await context.SaveChangesAsync();
        }
    }
}
