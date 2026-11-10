namespace POS.Core.Entities;

/// <summary>
/// An individual tax component (e.g. "VAT 15%", "Service Tax 5%").
/// IsCompound: calculated on top of the already-taxed subtotal.
/// IsInclusive: price already contains this tax — back-calculate to extract.
/// </summary>
public class TaxComponent : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public decimal Rate { get; set; }
    public bool IsCompound { get; set; } = false;
    public bool IsInclusive { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public ICollection<TaxGroupComponent> TaxGroups { get; set; } = new List<TaxGroupComponent>();
}
