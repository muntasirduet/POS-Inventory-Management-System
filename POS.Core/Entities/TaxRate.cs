namespace POS.Core.Entities;
public class TaxRate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public bool IsDefault { get; set; } = false;
    public string? AppliesTo { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
