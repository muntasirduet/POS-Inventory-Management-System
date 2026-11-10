namespace POS.Application.DTOs;
public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    // Legacy single-rate
    public int? TaxRateId { get; set; }
    public string? TaxRateName { get; set; }
    public decimal? TaxRateValue { get; set; }
    // TaxGroup — preferred
    public int? TaxGroupId { get; set; }
    public string? TaxGroupName { get; set; }
    /// <summary>Combined exclusive tax rate for POS display / cart calculation.</summary>
    public decimal EffectiveTaxRate { get; set; }
    /// <summary>True if any TaxGroup component is inclusive (tax baked into the displayed price).</summary>
    public bool TaxIsInclusive { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public int StockQty { get; set; }
    // Auto-discount from a scheduled Discount record for the product's category
    public string? AutoDiscountType { get; set; }   // "Percentage" | "FixedAmount" | null
    public decimal AutoDiscountValue { get; set; }
    /// <summary>True when this product has at least one active variant — POS will show picker.</summary>
    public bool HasVariants { get; set; }
}
