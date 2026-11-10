namespace POS.Core.Entities;

/// <summary>
/// Tracks stock for a specific product variant per branch.
/// This allows different branches to have different quantities for the same variant/attribute combination.
/// </summary>
public class VariantStockItem : BaseEntity
{
    public int VariantId { get; set; }
    public ProductVariant Variant { get; set; } = null!;
    
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    
    /// <summary>Quantity on hand for this variant at this branch.</summary>
    public int QtyOnHand { get; set; } = 0;
    
    /// <summary>Reorder level for this variant at this branch.</summary>
    public int ReorderLevel { get; set; } = 10;
}
