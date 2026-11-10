namespace POS.Core.Entities;

/// <summary>Stores a variant-level attribute value (e.g. Variant → Size = "L", Color = "Red").</summary>
public class VariantAttributeValue : BaseEntity
{
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition AttributeDefinition { get; set; } = null!;
    public string Value { get; set; } = string.Empty;
}
