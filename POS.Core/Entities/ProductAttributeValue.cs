namespace POS.Core.Entities;

/// <summary>Stores a product-level attribute value (e.g. Product → Brand = "Nike", Material = "Cotton").</summary>
public class ProductAttributeValue : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition AttributeDefinition { get; set; } = null!;
    public string Value { get; set; } = string.Empty;
}
