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
        ("sales.return", "Sales", "Process sale returns/refunds"),
        // Shifts
        ("shifts.manage", "Shifts", "Open, close and manage shifts"),
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
        // Pricing
        ("pricing.manage",  "Pricing", "Manage customer groups and price rules"),
        // Gift Cards
        ("giftcards.manage", "GiftCards", "Issue and manage gift cards"),
        // Store Credit
        ("storecredit.manage", "StoreCredit", "Manage customer store credit"),
    };

    // Role → list of permission names
    private static readonly Dictionary<string, string[]> RolePermissionMatrix = new()
    {
        ["SuperAdmin"] = PermissionDefinitions.Select(p => p.Name).ToArray(),

        ["StoreOwner"] = new[]
        {
            "products.view", "products.create", "products.edit", "products.delete",
            "categories.view", "categories.create", "categories.edit", "categories.delete",
            "sales.view", "sales.create", "sales.void", "sales.return",
            "inventory.view", "inventory.adjust", "inventory.transfer", "inventory.purchase",
            "suppliers.view", "suppliers.create", "suppliers.edit", "suppliers.delete",
            "customers.view", "customers.create", "customers.edit", "customers.delete",
            "reports.view", "reports.export", "reports.audit",
            "users.manage", "branches.manage", "settings.manage", "shifts.manage",
            "pricing.manage", "giftcards.manage", "storecredit.manage",
        },

        ["StoreManager"] = new[]
        {
            "products.view", "products.create", "products.edit",
            "categories.view", "categories.create", "categories.edit",
            "sales.view", "sales.create", "sales.return",
            "inventory.view", "inventory.adjust", "inventory.transfer", "inventory.purchase",
            "suppliers.view", "suppliers.create", "suppliers.edit",
            "customers.view", "customers.create", "customers.edit",
            "reports.view", "reports.export",
            "shifts.manage",
            "giftcards.manage", "storecredit.manage",
        },

        ["Cashier"] = new[]
        {
            "products.view",
            "categories.view",
            "sales.view", "sales.create", "sales.return",
            "customers.view", "customers.create", "customers.edit",
            "shifts.manage",
            "giftcards.manage", "storecredit.manage",
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
                new SystemConfig { Key = "LoyaltyPointsPerDollar", Value = "1",    Category = "Loyalty",    Description = "Points per dollar spent" },
                new SystemConfig { Key = "LoyaltyTierSilver",      Value = "500",  Category = "Loyalty",    Description = "Total spend ($) to reach Silver tier" },
                new SystemConfig { Key = "LoyaltyTierGold",        Value = "2000", Category = "Loyalty",    Description = "Total spend ($) to reach Gold tier" },
                new SystemConfig { Key = "LoyaltyTierPlatinum",    Value = "5000", Category = "Loyalty",    Description = "Total spend ($) to reach Platinum tier" },
                new SystemConfig { Key = "LowStockThreshold",      Value = "10",   Category = "Inventory",  Description = "Default low-stock threshold" },
                new SystemConfig { Key = "StoreName",              Value = "My POS Store", Category = "Store", Description = "Store display name" },
                new SystemConfig { Key = "ReceiptFooter",          Value = "Thank you for your purchase!", Category = "Store", Description = "Receipt footer" },
                new SystemConfig { Key = "HoldExpiryHours",        Value = "8",    Category = "Sales",     Description = "Hours before held sales expire" },
                new SystemConfig { Key = "GiftCardPrefix",         Value = "GC",   Category = "GiftCards", Description = "Gift card code prefix" },
                new SystemConfig { Key = "DefaultTaxRate",         Value = "15",   Category = "Tax",       Description = "Default tax rate percentage" }
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

        // Seed 50+ dummy products
        if (!await context.Products.AnyAsync())
        {
            var categories = await context.Categories.ToListAsync();
            var electronicsId = categories.FirstOrDefault(c => c.Name == "Electronics")?.Id ?? 1;
            var foodId = categories.FirstOrDefault(c => c.Name == "Food & Beverage")?.Id ?? 1;
            var clothingId = categories.FirstOrDefault(c => c.Name == "Clothing")?.Id ?? 1;
            var generalId = categories.FirstOrDefault(c => c.Name == "General")?.Id ?? 1;

            var products = new List<Product>
            {
                // Electronics - Phones (5)
                new Product { Name = "iPhone 15 Pro", SKU = "IPHONE15PRO", Barcode = "895691006850", CategoryId = electronicsId, Price = 999.99m, CostPrice = 750m, Description = "Premium flagship smartphone with A17 Pro chip", ImageUrl = "https://via.placeholder.com/300x300?text=iPhone+15+Pro", IsActive = true },
                new Product { Name = "Samsung Galaxy S24", SKU = "SAMS24", Barcode = "895691006851", CategoryId = electronicsId, Price = 899.99m, CostPrice = 680m, Description = "Latest Samsung flagship with AI features", ImageUrl = "https://via.placeholder.com/300x300?text=Galaxy+S24", IsActive = true },
                new Product { Name = "Google Pixel 8", SKU = "PIXEL8", Barcode = "895691006852", CategoryId = electronicsId, Price = 799.99m, CostPrice = 600m, Description = "Google's latest AI-powered smartphone", ImageUrl = "https://via.placeholder.com/300x300?text=Pixel+8", IsActive = true },
                new Product { Name = "OnePlus 12", SKU = "ONEPLUS12", Barcode = "895691006853", CategoryId = electronicsId, Price = 649.99m, CostPrice = 490m, Description = "Fast charging flagship killer", ImageUrl = "https://via.placeholder.com/300x300?text=OnePlus+12", IsActive = true },
                new Product { Name = "Xiaomi 14", SKU = "XIAOMI14", Barcode = "895691006854", CategoryId = electronicsId, Price = 749.99m, CostPrice = 560m, Description = "Powerful camera and performance", ImageUrl = "https://via.placeholder.com/300x300?text=Xiaomi+14", IsActive = true },
                
                // Electronics - Laptops (5)
                new Product { Name = "MacBook Pro 16\"", SKU = "MBP16M3", Barcode = "895691006855", CategoryId = electronicsId, Price = 2499.99m, CostPrice = 1875m, Description = "M3 Max processor, 16GB RAM", ImageUrl = "https://via.placeholder.com/300x300?text=MacBook+Pro", IsActive = true },
                new Product { Name = "Dell XPS 15", SKU = "DELLXPS15", Barcode = "895691006856", CategoryId = electronicsId, Price = 1799.99m, CostPrice = 1350m, Description = "4K OLED display, RTX 4070", ImageUrl = "https://via.placeholder.com/300x300?text=Dell+XPS", IsActive = true },
                new Product { Name = "HP Pavilion 15", SKU = "HPPAV15", Barcode = "895691006857", CategoryId = electronicsId, Price = 599.99m, CostPrice = 450m, Description = "Budget-friendly 15\" laptop", ImageUrl = "https://via.placeholder.com/300x300?text=HP+Pavilion", IsActive = true },
                new Product { Name = "ASUS VivoBook 14", SKU = "ASUSVIVOBOOK", Barcode = "895691006858", CategoryId = electronicsId, Price = 699.99m, CostPrice = 525m, Description = "Lightweight and portable", ImageUrl = "https://via.placeholder.com/300x300?text=ASUS+VivoBook", IsActive = true },
                new Product { Name = "Lenovo ThinkPad X1", SKU = "LENOVOX1", Barcode = "895691006859", CategoryId = electronicsId, Price = 1299.99m, CostPrice = 975m, Description = "Business laptop with security", ImageUrl = "https://via.placeholder.com/300x300?text=ThinkPad+X1", IsActive = true },

                // Electronics - Tablets (5)
                new Product { Name = "iPad Pro 12.9\"", SKU = "IPADPRO129", Barcode = "895691006860", CategoryId = electronicsId, Price = 1099.99m, CostPrice = 825m, Description = "M2 chip, stunning display", ImageUrl = "https://via.placeholder.com/300x300?text=iPad+Pro", IsActive = true },
                new Product { Name = "Samsung Tab S9 Ultra", SKU = "TABS9ULTRA", Barcode = "895691006861", CategoryId = electronicsId, Price = 1199.99m, CostPrice = 900m, Description = "14.6\" display, Snapdragon 8 Gen 2", ImageUrl = "https://via.placeholder.com/300x300?text=Tab+S9", IsActive = true },
                new Product { Name = "iPad Air", SKU = "IPADAIR", Barcode = "895691006862", CategoryId = electronicsId, Price = 699.99m, CostPrice = 525m, Description = "Mid-range iPad with M1", ImageUrl = "https://via.placeholder.com/300x300?text=iPad+Air", IsActive = true },
                new Product { Name = "Microsoft Surface Pro 9", SKU = "SURFACEPRO9", Barcode = "895691006863", CategoryId = electronicsId, Price = 1299.99m, CostPrice = 975m, Description = "2-in-1 laptop tablet", ImageUrl = "https://via.placeholder.com/300x300?text=Surface+Pro", IsActive = true },
                new Product { Name = "OnePlus Pad", SKU = "ONOPAD", Barcode = "895691006864", CategoryId = electronicsId, Price = 479.99m, CostPrice = 360m, Description = "High refresh rate tablet", ImageUrl = "https://via.placeholder.com/300x300?text=OnePlus+Pad", IsActive = true },

                // Electronics - Cameras (5)
                new Product { Name = "Canon EOS R5", SKU = "ENOSR5", Barcode = "895691006865", CategoryId = electronicsId, Price = 3899.99m, CostPrice = 2925m, Description = "Professional mirrorless camera", ImageUrl = "https://via.placeholder.com/300x300?text=Canon+R5", IsActive = true },
                new Product { Name = "Sony A7IV", SKU = "SONYA7IV", Barcode = "895691006866", CategoryId = electronicsId, Price = 2498.00m, CostPrice = 1873.50m, Description = "Full-frame mirrorless", ImageUrl = "https://via.placeholder.com/300x300?text=Sony+A7", IsActive = true },
                new Product { Name = "Nikon Z6II", SKU = "NIKOZ6II", Barcode = "895691006867", CategoryId = electronicsId, Price = 1999.95m, CostPrice = 1500m, Description = "Entry-level full-frame", ImageUrl = "https://via.placeholder.com/300x300?text=Nikon+Z6", IsActive = true },
                new Product { Name = "GoPro Hero 12", SKU = "GOPROHERO12", Barcode = "895691006868", CategoryId = electronicsId, Price = 499.99m, CostPrice = 375m, Description = "Action camera 5.3K", ImageUrl = "https://via.placeholder.com/300x300?text=GoPro+Hero", IsActive = true },
                new Product { Name = "DJI Mavic 3", SKU = "DJIMAVIC3", Barcode = "895691006869", CategoryId = electronicsId, Price = 2199.00m, CostPrice = 1650m, Description = "Pro drone with Hasselblad camera", ImageUrl = "https://via.placeholder.com/300x300?text=DJI+Mavic", IsActive = true },

                // Food & Beverage (10)
                new Product { Name = "Coca Cola 500ml", SKU = "COCACOLA500", Barcode = "895691006870", CategoryId = foodId, Price = 2.99m, CostPrice = 1.50m, Description = "Classic cola drink", ImageUrl = "https://via.placeholder.com/300x300?text=Coca+Cola", IsActive = true },
                new Product { Name = "Pepsi 500ml", SKU = "PEPSI500", Barcode = "895691006871", CategoryId = foodId, Price = 2.99m, CostPrice = 1.50m, Description = "Refreshing cola", ImageUrl = "https://via.placeholder.com/300x300?text=Pepsi", IsActive = true },
                new Product { Name = "Orange Juice 1L", SKU = "OJ1L", Barcode = "895691006872", CategoryId = foodId, Price = 3.99m, CostPrice = 1.99m, Description = "Fresh orange juice", ImageUrl = "https://via.placeholder.com/300x300?text=Orange+Juice", IsActive = true },
                new Product { Name = "Apple Juice 1L", SKU = "AJ1L", Barcode = "895691006873", CategoryId = foodId, Price = 3.99m, CostPrice = 1.99m, Description = "Pure apple juice", ImageUrl = "https://via.placeholder.com/300x300?text=Apple+Juice", IsActive = true },
                new Product { Name = "Sprite 500ml", SKU = "SPRITE500", Barcode = "895691006874", CategoryId = foodId, Price = 2.99m, CostPrice = 1.50m, Description = "Lemon-lime soda", ImageUrl = "https://via.placeholder.com/300x300?text=Sprite", IsActive = true },
                new Product { Name = "Milk 1L", SKU = "MILK1L", Barcode = "895691006875", CategoryId = foodId, Price = 4.49m, CostPrice = 2.25m, Description = "Whole milk", ImageUrl = "https://via.placeholder.com/300x300?text=Milk", IsActive = true },
                new Product { Name = "Coffee 250g", SKU = "COFFEE250", Barcode = "895691006876", CategoryId = foodId, Price = 8.99m, CostPrice = 4.50m, Description = "Premium ground coffee", ImageUrl = "https://via.placeholder.com/300x300?text=Coffee", IsActive = true },
                new Product { Name = "Tea Bags 20pcs", SKU = "TEABAGS20", Barcode = "895691006877", CategoryId = foodId, Price = 5.99m, CostPrice = 3.00m, Description = "Assorted tea bags", ImageUrl = "https://via.placeholder.com/300x300?text=Tea+Bags", IsActive = true },
                new Product { Name = "Bread 500g", SKU = "BREAD500", Barcode = "895691006878", CategoryId = foodId, Price = 3.49m, CostPrice = 1.75m, Description = "Whole wheat bread", ImageUrl = "https://via.placeholder.com/300x300?text=Bread", IsActive = true },
                new Product { Name = "Cheese Pack 200g", SKU = "CHEESE200", Barcode = "895691006879", CategoryId = foodId, Price = 6.99m, CostPrice = 3.50m, Description = "Sliced cheddar cheese", ImageUrl = "https://via.placeholder.com/300x300?text=Cheese", IsActive = true },

                // Clothing (15)
                new Product { Name = "Blue Cotton T-Shirt M", SKU = "TSHIRT-BLUE-M", Barcode = "895691006880", CategoryId = clothingId, Price = 24.99m, CostPrice = 12.50m, Description = "100% cotton blue t-shirt", ImageUrl = "https://via.placeholder.com/300x300?text=Blue+TShirt", IsActive = true },
                new Product { Name = "Black Jeans 32", SKU = "JEANS-BLK-32", Barcode = "895691006881", CategoryId = clothingId, Price = 59.99m, CostPrice = 30.00m, Description = "Black denim jeans", ImageUrl = "https://via.placeholder.com/300x300?text=Black+Jeans", IsActive = true },
                new Product { Name = "White Dress Shirt L", SKU = "DSHIRT-WHT-L", Barcode = "895691006882", CategoryId = clothingId, Price = 49.99m, CostPrice = 25.00m, Description = "Formal white dress shirt", ImageUrl = "https://via.placeholder.com/300x300?text=Dress+Shirt", IsActive = true },
                new Product { Name = "Red Polo Shirt S", SKU = "POLO-RED-S", Barcode = "895691006883", CategoryId = clothingId, Price = 34.99m, CostPrice = 17.50m, Description = "Classic red polo", ImageUrl = "https://via.placeholder.com/300x300?text=Polo+Red", IsActive = true },
                new Product { Name = "Gray Hoodie XL", SKU = "HOODIE-GRY-XL", Barcode = "895691006884", CategoryId = clothingId, Price = 54.99m, CostPrice = 27.50m, Description = "Comfortable gray hoodie", ImageUrl = "https://via.placeholder.com/300x300?text=Gray+Hoodie", IsActive = true },
                new Product { Name = "Black Jacket M", SKU = "JACKET-BLK-M", Barcode = "895691006885", CategoryId = clothingId, Price = 89.99m, CostPrice = 45.00m, Description = "Leather black jacket", ImageUrl = "https://via.placeholder.com/300x300?text=Black+Jacket", IsActive = true },
                new Product { Name = "Khaki Shorts M", SKU = "SHORTS-KHK-M", Barcode = "895691006886", CategoryId = clothingId, Price = 39.99m, CostPrice = 20.00m, Description = "Casual khaki shorts", ImageUrl = "https://via.placeholder.com/300x300?text=Khaki+Shorts", IsActive = true },
                new Product { Name = "Blue Jeans Dress L", SKU = "JEANSDRESS-BLU-L", Barcode = "895691006887", CategoryId = clothingId, Price = 64.99m, CostPrice = 32.50m, Description = "Casual denim dress", ImageUrl = "https://via.placeholder.com/300x300?text=Denim+Dress", IsActive = true },
                new Product { Name = "White Sneakers 10", SKU = "SNEAKERS-WHT-10", Barcode = "895691006888", CategoryId = clothingId, Price = 79.99m, CostPrice = 40.00m, Description = "Athletic white sneakers", ImageUrl = "https://via.placeholder.com/300x300?text=White+Sneakers", IsActive = true },
                new Product { Name = "Black Formal Shoes 9", SKU = "SHOES-BLK-9", Barcode = "895691006889", CategoryId = clothingId, Price = 99.99m, CostPrice = 50.00m, Description = "Formal black leather shoes", ImageUrl = "https://via.placeholder.com/300x300?text=Formal+Shoes", IsActive = true },
                new Product { Name = "Summer Dress S", SKU = "SUMMER-DRESS-S", Barcode = "895691006890", CategoryId = clothingId, Price = 44.99m, CostPrice = 22.50m, Description = "Floral summer dress", ImageUrl = "https://via.placeholder.com/300x300?text=Summer+Dress", IsActive = true },
                new Product { Name = "Winter Coat L", SKU = "COAT-WIN-L", Barcode = "895691006891", CategoryId = clothingId, Price = 149.99m, CostPrice = 75.00m, Description = "Warm winter coat", ImageUrl = "https://via.placeholder.com/300x300?text=Winter+Coat", IsActive = true },
                new Product { Name = "Socks Pack 5", SKU = "SOCKS-5PK", Barcode = "895691006892", CategoryId = clothingId, Price = 14.99m, CostPrice = 7.50m, Description = "Pack of 5 socks", ImageUrl = "https://via.placeholder.com/300x300?text=Socks+Pack", IsActive = true },
                new Product { Name = "Cap Black", SKU = "CAP-BLK", Barcode = "895691006893", CategoryId = clothingId, Price = 24.99m, CostPrice = 12.50m, Description = "Classic black cap", ImageUrl = "https://via.placeholder.com/300x300?text=Cap+Black", IsActive = true },
                new Product { Name = "Scarf Wool Gray", SKU = "SCARF-GRAY", Barcode = "895691006894", CategoryId = clothingId, Price = 34.99m, CostPrice = 17.50m, Description = "Warm wool scarf", ImageUrl = "https://via.placeholder.com/300x300?text=Scarf+Gray", IsActive = true },

                // General (10)
                new Product { Name = "USB-C Cable 2m", SKU = "USBC-2M", Barcode = "895691006895", CategoryId = generalId, Price = 12.99m, CostPrice = 6.50m, Description = "Fast charging USB-C cable", ImageUrl = "https://via.placeholder.com/300x300?text=USB-C+Cable", IsActive = true },
                new Product { Name = "Phone Case iPhone 15", SKU = "CASE-IP15", Barcode = "895691006896", CategoryId = generalId, Price = 19.99m, CostPrice = 10.00m, Description = "Protective phone case", ImageUrl = "https://via.placeholder.com/300x300?text=Phone+Case", IsActive = true },
                new Product { Name = "Screen Protector", SKU = "SCREEN-PROT", Barcode = "895691006897", CategoryId = generalId, Price = 9.99m, CostPrice = 5.00m, Description = "Tempered glass screen protector", ImageUrl = "https://via.placeholder.com/300x300?text=Screen+Protector", IsActive = true },
                new Product { Name = "Power Bank 20000mAh", SKU = "POWERBANK-20K", Barcode = "895691006898", CategoryId = generalId, Price = 39.99m, CostPrice = 20.00m, Description = "Fast charging power bank", ImageUrl = "https://via.placeholder.com/300x300?text=Power+Bank", IsActive = true },
                new Product { Name = "Wireless Charger", SKU = "WIRELESS-CHAR", Barcode = "895691006899", CategoryId = generalId, Price = 29.99m, CostPrice = 15.00m, Description = "15W wireless charger", ImageUrl = "https://via.placeholder.com/300x300?text=Wireless+Charger", IsActive = true },
                new Product { Name = "Headphone Stand", SKU = "HP-STAND", Barcode = "895691006900", CategoryId = generalId, Price = 14.99m, CostPrice = 7.50m, Description = "Aluminum headphone stand", ImageUrl = "https://via.placeholder.com/300x300?text=HP+Stand", IsActive = true },
                new Product { Name = "Laptop Stand", SKU = "LAPTOP-STAND", Barcode = "895691006901", CategoryId = generalId, Price = 44.99m, CostPrice = 22.50m, Description = "Adjustable laptop stand", ImageUrl = "https://via.placeholder.com/300x300?text=Laptop+Stand", IsActive = true },
                new Product { Name = "HDMI Cable 3m", SKU = "HDMI-3M", Barcode = "895691006902", CategoryId = generalId, Price = 16.99m, CostPrice = 8.50m, Description = "High-speed HDMI cable", ImageUrl = "https://via.placeholder.com/300x300?text=HDMI+Cable", IsActive = true },
                new Product { Name = "USB Hub 4 Port", SKU = "USBHUB-4", Barcode = "895691006903", CategoryId = generalId, Price = 24.99m, CostPrice = 12.50m, Description = "4-port USB hub", ImageUrl = "https://via.placeholder.com/300x300?text=USB+Hub", IsActive = true },
                new Product { Name = "SD Card 128GB", SKU = "SDCARD-128", Barcode = "895691006904", CategoryId = generalId, Price = 34.99m, CostPrice = 17.50m, Description = "High-speed SD card", ImageUrl = "https://via.placeholder.com/300x300?text=SD+Card", IsActive = true }
            };

            context.Products.AddRange(products);
            await context.SaveChangesAsync();
        }
    }
}
