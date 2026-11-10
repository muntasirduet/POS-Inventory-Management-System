namespace POS.Core.Entities;
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    // Legacy single-rate kept for backward compat; preferred path is TaxGroupId
    public int? TaxRateId { get; set; }
    public TaxRate? TaxRate { get; set; }
    public int? TaxGroupId { get; set; }
    public TaxGroup? TaxGroup { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    /// <summary>Purchase/cost price — used for COGS and margin reports.</summary>
    public decimal CostPrice { get; set; } = 0;
    /// <summary>"Product" = track stock at branch level; "Variant" = track stock per variant SKU.</summary>
    public string InventoryTracking { get; set; } = "Product";
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    public ICollection<StockItem> StockItems { get; set; } = new List<StockItem>();
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
    public ICollection<SaleReturnItem> SaleReturnItems { get; set; } = new List<SaleReturnItem>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<PriceRule> PriceRules { get; set; } = new List<PriceRule>();
    public ICollection<ProductAttributeValue> AttributeValues { get; set; } = new List<ProductAttributeValue>();
}
