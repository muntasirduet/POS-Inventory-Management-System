namespace POS.Core.Entities;

/// <summary>
/// Overrides price for a specific product/category for a customer group.
/// Priority: higher number wins. Applied before tax, after subtotal.
/// </summary>
public class PriceRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int? CustomerGroupId { get; set; }
    public CustomerGroup? CustomerGroup { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    /// <summary>Percentage | FixedAmount | FixedPrice</summary>
    public string DiscountType { get; set; } = "Percentage";
    public decimal Value { get; set; }
    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
