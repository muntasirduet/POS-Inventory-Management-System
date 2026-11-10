namespace POS.Application.DTOs;

/// <summary>Represents a variant with its stock availability across branches.</summary>
public class VariantWithStockDto
{
    public int Id { get; set; }
    public string? Label { get; set; }
    public string? SKU { get; set; }
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    
    /// <summary>Variant attributes (e.g. Color, Size with their values).</summary>
    public List<VariantAttributeDto> Attributes { get; set; } = new();
    
    /// <summary>Stock available per branch.</summary>
    public List<BranchVariantStockDto> StockByBranch { get; set; } = new();
}

public class VariantAttributeDto
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class BranchVariantStockDto
{
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; }
}
