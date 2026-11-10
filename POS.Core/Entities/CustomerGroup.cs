namespace POS.Core.Entities;

/// <summary>Customer segment (e.g. VIP, Wholesale, Staff) — can drive price rules.</summary>
public class CustomerGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Default flat % discount applied to all sales for members of this group.</summary>
    public decimal DefaultDiscountPct { get; set; } = 0;
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<PriceRule> PriceRules { get; set; } = new List<PriceRule>();
}
