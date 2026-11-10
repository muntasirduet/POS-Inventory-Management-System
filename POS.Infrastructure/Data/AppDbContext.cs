using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Identity;

namespace POS.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<TaxGroup> TaxGroups => Set<TaxGroup>();
    public DbSet<TaxComponent> TaxComponents => Set<TaxComponent>();
    public DbSet<TaxGroupComponent> TaxGroupComponents => Set<TaxGroupComponent>();
    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
    public DbSet<AttributeOption> AttributeOptions => Set<AttributeOption>();
    public DbSet<CategoryAttributeTemplate> CategoryAttributeTemplates => Set<CategoryAttributeTemplate>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<VariantAttributeValue> VariantAttributeValues => Set<VariantAttributeValue>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<VariantStockItem> VariantStockItems => Set<VariantStockItem>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Reward> Rewards => Set<Reward>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnItem> SaleReturnItems => Set<SaleReturnItem>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftCashEntry> ShiftCashEntries => Set<ShiftCashEntry>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ReceiptTemplate> ReceiptTemplates => Set<ReceiptTemplate>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    // Phase 3
    public DbSet<CustomerGroup> CustomerGroups => Set<CustomerGroup>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();
    public DbSet<GiftCard> GiftCards => Set<GiftCard>();
    public DbSet<StoreCreditAccount> StoreCreditAccounts => Set<StoreCreditAccount>();
    public DbSet<StoreCreditTransaction> StoreCreditTransactions => Set<StoreCreditTransaction>();
    // Phase 4
    public DbSet<ShiftDenomination> ShiftDenominations => Set<ShiftDenomination>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>().Property(p => p.Price).HasColumnType("decimal(18,4)");
        builder.Entity<ProductVariant>().Property(p => p.Price).HasColumnType("decimal(18,4)");
        builder.Entity<Sale>().Property(s => s.SubTotal).HasColumnType("decimal(18,4)");
        builder.Entity<Sale>().Property(s => s.Tax).HasColumnType("decimal(18,4)");
        builder.Entity<Sale>().Property(s => s.Discount).HasColumnType("decimal(18,4)");
        builder.Entity<Sale>().Property(s => s.Total).HasColumnType("decimal(18,4)");
        builder.Entity<SaleItem>().Property(si => si.UnitPrice).HasColumnType("decimal(18,4)");
        builder.Entity<SaleItem>().Property(si => si.Discount).HasColumnType("decimal(18,4)");
        builder.Entity<SaleItem>().Property(si => si.LineTotal).HasColumnType("decimal(18,4)");
        builder.Entity<SaleItem>()
            .HasOne(si => si.Sale).WithMany(s => s.Items)
            .HasForeignKey(si => si.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<SaleItem>()
            .HasOne(si => si.Product).WithMany(p => p.SaleItems)
            .HasForeignKey(si => si.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SaleItem>()
            .HasOne(si => si.Variant).WithMany()
            .HasForeignKey(si => si.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Payment>().Property(p => p.Amount).HasColumnType("decimal(18,4)");
        builder.Entity<Payment>().Property(p => p.Change).HasColumnType("decimal(18,4)");
        builder.Entity<Discount>().Property(d => d.Value).HasColumnType("decimal(18,4)");
        builder.Entity<Coupon>().Property(c => c.Value).HasColumnType("decimal(18,4)");
        builder.Entity<Reward>().Property(r => r.DiscountValue).HasColumnType("decimal(18,4)");
        builder.Entity<TaxRate>().Property(t => t.Rate).HasColumnType("decimal(18,4)");
        builder.Entity<PurchaseOrder>().Property(po => po.Total).HasColumnType("decimal(18,4)");
        builder.Entity<PurchaseOrderItem>().Property(poi => poi.UnitCost).HasColumnType("decimal(18,4)");
        builder.Entity<PurchaseOrderItem>().Property(poi => poi.LineTotal).HasColumnType("decimal(18,4)");
        builder.Entity<LoyaltyAccount>().Property(la => la.TotalSpend).HasColumnType("decimal(18,4)");
        builder.Entity<Employee>().Property(e => e.CommissionPercent).HasColumnType("decimal(18,4)");
        builder.Entity<SaleReturn>().Property(sr => sr.RefundAmount).HasColumnType("decimal(18,4)");
        // SaleReturnItem
        builder.Entity<SaleReturnItem>().Property(sri => sri.UnitPrice).HasColumnType("decimal(18,4)");
        builder.Entity<SaleReturnItem>()
            .HasOne(sri => sri.SaleReturn).WithMany(sr => sr.Items)
            .HasForeignKey(sri => sri.SaleReturnId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<SaleReturnItem>()
            .HasOne(sri => sri.Product).WithMany(p => p.SaleReturnItems)
            .HasForeignKey(sri => sri.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SaleReturnItem>()
            .HasOne(sri => sri.SaleItem).WithMany()
            .HasForeignKey(sri => sri.SaleItemId).OnDelete(DeleteBehavior.Restrict);
        // Shift & ShiftCashEntry
        builder.Entity<Shift>().Property(s => s.OpeningCash).HasColumnType("decimal(18,4)");
        builder.Entity<Shift>().Property(s => s.ClosingCash).HasColumnType("decimal(18,4)");
        builder.Entity<Shift>().Property(s => s.ExpectedCash).HasColumnType("decimal(18,4)");
        builder.Entity<Shift>()
            .HasOne(s => s.Branch).WithMany().HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<ShiftCashEntry>().Property(sce => sce.Amount).HasColumnType("decimal(18,4)");
        builder.Entity<ShiftCashEntry>()
            .HasOne(sce => sce.Shift).WithMany(s => s.CashEntries)
            .HasForeignKey(sce => sce.ShiftId).OnDelete(DeleteBehavior.Cascade);
        // LoyaltyTransaction
        builder.Entity<LoyaltyTransaction>()
            .HasOne(lt => lt.LoyaltyAccount).WithMany(la => la.Transactions)
            .HasForeignKey(lt => lt.LoyaltyAccountId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<LoyaltyTransaction>()
            .HasOne(lt => lt.Reward).WithMany()
            .HasForeignKey(lt => lt.RewardId).OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Product>().HasIndex(p => p.SKU).IsUnique();
        builder.Entity<Product>().HasIndex(p => p.Barcode);
        builder.Entity<Sale>().HasIndex(s => s.SaleNumber).IsUnique();
        builder.Entity<Sale>().HasIndex(s => s.CreatedAt);
        builder.Entity<Customer>().HasIndex(c => c.Phone);
        builder.Entity<Coupon>().HasIndex(c => c.Code).IsUnique();
        builder.Entity<StockItem>().HasIndex(si => new { si.ProductId, si.BranchId }).IsUnique();
        builder.Entity<VariantStockItem>().HasIndex(vsi => new { vsi.VariantId, vsi.BranchId }).IsUnique();
        builder.Entity<VariantStockItem>()
            .HasOne(vsi => vsi.Variant).WithMany()
            .HasForeignKey(vsi => vsi.VariantId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<VariantStockItem>()
            .HasOne(vsi => vsi.Branch).WithMany()
            .HasForeignKey(vsi => vsi.BranchId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<TaxGroupComponent>().HasKey(tgc => new { tgc.TaxGroupId, tgc.TaxComponentId });
        builder.Entity<TaxComponent>().Property(tc => tc.Rate).HasColumnType("decimal(18,4)");

        // Dynamic attributes
        builder.Entity<CategoryAttributeTemplate>().HasKey(cat => new { cat.CategoryId, cat.AttributeDefinitionId });
        builder.Entity<ProductAttributeValue>().HasIndex(pav => new { pav.ProductId, pav.AttributeDefinitionId }).IsUnique();
        builder.Entity<VariantAttributeValue>().HasIndex(vav => new { vav.ProductVariantId, vav.AttributeDefinitionId }).IsUnique();

        // Sale hold fields
        builder.Entity<Sale>().Property(s => s.HoldDiscountAmount).HasColumnType("decimal(18,4)");
        builder.Entity<Sale>()
            .HasOne(s => s.Terminal).WithMany().HasForeignKey(s => s.TerminalId).OnDelete(DeleteBehavior.SetNull);

        builder.Entity<SaleItem>().Property(si => si.TaxRateSnapshot).HasColumnType("decimal(18,4)");

        // Product cost price
        builder.Entity<Product>().Property(p => p.CostPrice).HasColumnType("decimal(18,4)");
        builder.Entity<Product>()
            .HasOne(p => p.TaxGroup).WithMany(tg => tg.Products)
            .HasForeignKey(p => p.TaxGroupId).OnDelete(DeleteBehavior.SetNull);

        // Category extra fields
        builder.Entity<Category>()
            .HasOne(c => c.DefaultTaxGroup).WithMany()
            .HasForeignKey(c => c.DefaultTaxGroupId).OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
        builder.Entity<Category>().HasQueryFilter(c => !c.IsDeleted);
        builder.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);
        builder.Entity<Employee>().HasQueryFilter(e => !e.IsDeleted);

        builder.Entity<Category>()
            .HasOne(c => c.ParentCategory).WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Product>()
            .HasOne(p => p.Category).WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<StockMovement>()
            .HasOne(sm => sm.FromBranch).WithMany()
            .HasForeignKey(sm => sm.FromBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<StockMovement>()
            .HasOne(sm => sm.ToBranch).WithMany()
            .HasForeignKey(sm => sm.ToBranchId).OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Sale>()
            .HasOne(s => s.Customer).WithMany(c => c.Sales)
            .HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Receipt>()
            .HasOne(r => r.Sale).WithOne(s => s.Receipt)
            .HasForeignKey<Receipt>(r => r.SaleId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LoyaltyAccount>()
            .HasOne(la => la.Customer).WithOne(c => c.LoyaltyAccount)
            .HasForeignKey<LoyaltyAccount>(la => la.CustomerId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Membership>()
            .HasOne(m => m.Customer).WithOne(c => c.Membership)
            .HasForeignKey<Membership>(m => m.CustomerId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Employee>()
            .HasOne(e => e.Branch).WithMany()
            .HasForeignKey(e => e.BranchId).OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Permission>()
            .HasIndex(p => p.Name).IsUnique();

        builder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });
        builder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserPermissionOverride>()
            .HasKey(upo => new { upo.UserId, upo.PermissionId });
        builder.Entity<UserPermissionOverride>()
            .HasOne(upo => upo.Permission).WithMany(p => p.UserOverrides)
            .HasForeignKey(upo => upo.PermissionId).OnDelete(DeleteBehavior.Cascade);

        // CustomerGroup + PriceRule
        builder.Entity<Customer>()
            .HasOne(c => c.CustomerGroup).WithMany(cg => cg.Customers)
            .HasForeignKey(c => c.CustomerGroupId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<CustomerGroup>().Property(cg => cg.DefaultDiscountPct).HasColumnType("decimal(18,4)");
        builder.Entity<PriceRule>().Property(pr => pr.Value).HasColumnType("decimal(18,4)");
        builder.Entity<PriceRule>()
            .HasOne(pr => pr.CustomerGroup).WithMany(cg => cg.PriceRules)
            .HasForeignKey(pr => pr.CustomerGroupId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PriceRule>()
            .HasOne(pr => pr.Product).WithMany(p => p.PriceRules)
            .HasForeignKey(pr => pr.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<PriceRule>()
            .HasOne(pr => pr.Category).WithMany(c => c.PriceRules)
            .HasForeignKey(pr => pr.CategoryId).OnDelete(DeleteBehavior.Cascade);

        // GiftCard
        builder.Entity<GiftCard>().Property(gc => gc.InitialValue).HasColumnType("decimal(18,4)");
        builder.Entity<GiftCard>().Property(gc => gc.Balance).HasColumnType("decimal(18,4)");
        builder.Entity<GiftCard>().HasIndex(gc => gc.Code).IsUnique();
        builder.Entity<GiftCard>()
            .HasOne(gc => gc.IssuedToCustomer).WithMany()
            .HasForeignKey(gc => gc.IssuedToCustomerId).OnDelete(DeleteBehavior.SetNull);

        // StoreCredit
        builder.Entity<StoreCreditAccount>().Property(sc => sc.Balance).HasColumnType("decimal(18,4)");
        builder.Entity<StoreCreditAccount>().Property(sc => sc.TotalIssued).HasColumnType("decimal(18,4)");
        builder.Entity<StoreCreditAccount>()
            .HasOne(sc => sc.Customer).WithOne(c => c.StoreCreditAccount)
            .HasForeignKey<StoreCreditAccount>(sc => sc.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<StoreCreditTransaction>().Property(sct => sct.Amount).HasColumnType("decimal(18,4)");
        builder.Entity<StoreCreditTransaction>().Property(sct => sct.BalanceAfter).HasColumnType("decimal(18,4)");
        builder.Entity<StoreCreditTransaction>()
            .HasOne(sct => sct.Account).WithMany(sc => sc.Transactions)
            .HasForeignKey(sct => sct.StoreCreditAccountId).OnDelete(DeleteBehavior.Cascade);

        // ShiftDenomination
        builder.Entity<ShiftDenomination>().Property(sd => sd.DenominationValue).HasColumnType("decimal(18,4)");
        builder.Entity<ShiftDenomination>()
            .HasOne(sd => sd.Shift).WithMany(s => s.Denominations)
            .HasForeignKey(sd => sd.ShiftId).OnDelete(DeleteBehavior.Cascade);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && e.State == EntityState.Modified);
        foreach (var entry in entries)
            ((BaseEntity)entry.Entity).UpdatedAt = DateTime.UtcNow;
    }
}
