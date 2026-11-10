namespace POS.Core.Entities;

public class TaxGroupComponent
{
    public int TaxGroupId { get; set; }
    public TaxGroup TaxGroup { get; set; } = null!;
    public int TaxComponentId { get; set; }
    public TaxComponent TaxComponent { get; set; } = null!;
}
