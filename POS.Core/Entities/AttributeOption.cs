namespace POS.Core.Entities;

/// <summary>Pre-defined dropdown values for a Select-type attribute (e.g. "S", "M", "L", "XL" for Size).</summary>
public class AttributeOption : BaseEntity
{
    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition AttributeDefinition { get; set; } = null!;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}
