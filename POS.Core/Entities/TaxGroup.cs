namespace POS.Core.Entities;

/// <summary>
/// A named group of tax components (e.g. "Standard Retail" = VAT 15%).
/// Products link to a TaxGroup instead of a single TaxRate.
/// </summary>
public class TaxGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; } = false;
    public ICollection<TaxGroupComponent> Components { get; set; } = new List<TaxGroupComponent>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
