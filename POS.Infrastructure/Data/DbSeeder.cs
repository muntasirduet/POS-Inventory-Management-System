using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Identity;

namespace POS.Infrastructure.Data;

public static class DbSeeder
{
    public static readonly string[] Roles = { "SuperAdmin", "StoreOwner", "StoreManager", "Cashier", "InventoryManager", "Accountant" };

    // All granular permission names grouped by module
    private static readonly (string Name, string Module, string Description)[] PermissionDefinitions =
    {
        // Products
        ("products.view",    "Products", "View products"),
        ("products.create",  "Products", "Create products"),
        ("products.edit",    "Products", "Edit products"),
        ("products.delete",  "Products", "Delete products"),
        // Categories
        ("categories.view",   "Categories", "View categories"),
        ("categories.create", "Categories", "Create categories"),
        ("categories.edit",   "Categories", "Edit categories"),
        ("categories.delete", "Categories", "Delete categories"),
        // Sales
        ("sales.view",   "Sales", "View sales"),
        ("sales.create", "Sales", "Create sales"),
        ("sales.void",   "Sales", "Void/delete sales"),
        // Inventory
        ("inventory.view",     "Inventory", "View stock levels"),
        ("inventory.adjust",   "Inventory", "Adjust stock"),
        ("inventory.transfer", "Inventory", "Transfer stock between branches"),
        ("inventory.purchase", "Inventory", "Create purchase orders"),
        // Customers
        ("customers.view",   "Customers", "View customers"),
        ("customers.create", "Customers", "Create customers"),
        ("customers.edit",   "Customers", "Edit customers"),
        ("customers.delete", "Customers", "Delete customers"),
        // Reports
        ("reports.view",   "Reports", "View reports"),
        ("reports.export", "Reports", "Export reports"),
        ("reports.audit",  "Reports", "View audit logs"),
        // Suppliers
        ("suppliers.view",   "Suppliers", "View suppliers"),
        ("suppliers.create", "Suppliers", "Create suppliers"),
        ("suppliers.edit",   "Suppliers", "Edit suppliers"),
        ("suppliers.delete", "Suppliers", "Delete suppliers"),
        // Admin
        ("users.manage",    "Admin", "Create/edit/deactivate users"),
        ("roles.manage",    "Admin", "Create roles and manage role permissions"),
        ("branches.manage", "Admin", "Create and manage branches"),
        ("settings.manage", "Admin", "Manage system settings"),
    };

    // Role → list of permission names
    private static readonly Dictionary<string, string[]> RolePermissionMatrix = new()
    {
        ["SuperAdmin"] = PermissionDefinitions.Select(p => p.Name).ToArray(),

        ["StoreOwner"] = new[]
        {
            "products.view", "products.create", "products.edit", "products.delete",
            "categories.view", "categories.create", "categories.edit", "categories.delete",
            "sales.view", "sales.create", "sales.void",
            "inventory.view", "inventory.adjust", "inventory.transfer", "inventory.purchase",
            "suppliers.view", "suppliers.create", "suppliers.edit", "suppliers.delete",
            "customers.view", "customers.create", "customers.edit", "customers.delete",
            "reports.view", "reports.export", "reports.audit",
            "users.manage", "branches.manage", "settings.manage",
        },

        ["StoreManager"] = new[]
        {
            "products.view", "products.create", "products.edit",
            "categories.view", "categories.create", "categories.edit",
            "sales.view", "sales.create",
            "inventory.view", "inventory.adjust", "inventory.transfer", "inventory.purchase",
            "suppliers.view", "suppliers.create", "suppliers.edit",
            "customers.view", "customers.create", "customers.edit",
            "reports.view", "reports.export",
        },

        ["Cashier"] = new[]
        {
            "products.view",
            "categories.view",
            "sales.view", "sales.create",
            "customers.view", "customers.create", "customers.edit",
        },

        ["InventoryManager"] = new[]
        {
            "products.view", "products.create", "products.edit",
            "categories.view",
            "inventory.view", "inventory.adjust", "inventory.transfer", "inventory.purchase",
            "suppliers.view", "suppliers.create", "suppliers.edit",
        },

        ["Accountant"] = new[]
        {
            "sales.view",
            "reports.view", "reports.export", "reports.audit",
        },
    };

    public static async Task SeedAsync(AppDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        await context.Database.MigrateAsync();

        // Seed roles
        foreach (var role in Roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // Seed default admin
        const string adminEmail = "admin@pos.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, FullName = "Super Admin", EmailConfirmed = true, IsActive = true };
            var result = await userManager.CreateAsync(admin, "Admin@123456");
            if (result.Succeeded) await userManager.AddToRoleAsync(admin, "SuperAdmin");
        }

        // Seed default store owner
        const string ownerEmail = "storeowner@pos.com";
        if (await userManager.FindByEmailAsync(ownerEmail) == null)
        {
            var owner = new ApplicationUser { UserName = ownerEmail, Email = ownerEmail, FullName = "Store Owner", EmailConfirmed = true, IsActive = true };
            var ownerResult = await userManager.CreateAsync(owner, "Owner@123456");
            if (ownerResult.Succeeded) await userManager.AddToRoleAsync(owner, "StoreOwner");
        }

        // Seed permissions
        foreach (var (name, module, description) in PermissionDefinitions)
        {
            if (!await context.Permissions.AnyAsync(p => p.Name == name))
                context.Permissions.Add(new Permission { Name = name, Module = module, Description = description });
        }
        await context.SaveChangesAsync();

        // Seed role-permission assignments
        var allPermissions = await context.Permissions.ToListAsync();
        var permLookup = allPermissions.ToDictionary(p => p.Name);

        foreach (var (roleName, permNames) in RolePermissionMatrix)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null) continue;

            foreach (var permName in permNames)
            {
                if (!permLookup.TryGetValue(permName, out var perm)) continue;
                var exists = await context.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == perm.Id);
                if (!exists)
                    context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            }
        }
        await context.SaveChangesAsync();

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
