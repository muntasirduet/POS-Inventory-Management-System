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
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
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
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ReceiptTemplate> ReceiptTemplates => Set<ReceiptTemplate>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

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
        builder.Entity<Shift>().Property(s => s.OpeningCash).HasColumnType("decimal(18,4)");
        builder.Entity<Shift>().Property(s => s.ClosingCash).HasColumnType("decimal(18,4)");

        builder.Entity<Product>().HasIndex(p => p.SKU).IsUnique();
        builder.Entity<Product>().HasIndex(p => p.Barcode);
        builder.Entity<Sale>().HasIndex(s => s.SaleNumber).IsUnique();
        builder.Entity<Sale>().HasIndex(s => s.CreatedAt);
        builder.Entity<Customer>().HasIndex(c => c.Phone);
        builder.Entity<Coupon>().HasIndex(c => c.Code).IsUnique();
        builder.Entity<StockItem>().HasIndex(si => new { si.ProductId, si.BranchId }).IsUnique();

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
