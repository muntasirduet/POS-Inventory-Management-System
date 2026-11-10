namespace POS.Core.Entities;
public class ProductVariant : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    /// <summary>Human-readable label auto-built from attribute values, e.g. "L / Red".</summary>
    public string? Label { get; set; }
    public decimal Price { get; set; }
    public int StockQty { get; set; }
    public string? SKU { get; set; }
    public string? Barcode { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<VariantAttributeValue> AttributeValues { get; set; } = new List<VariantAttributeValue>();
    /// <summary>Per-branch stock tracking for this variant.</summary>
    public ICollection<VariantStockItem> BranchStockItems { get; set; } = new List<VariantStockItem>();
}
