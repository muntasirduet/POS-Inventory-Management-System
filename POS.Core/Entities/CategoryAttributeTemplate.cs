namespace POS.Core.Entities;

/// <summary>Links an AttributeDefinition to a Category so that products in that category auto-show the attribute fields.</summary>
public class CategoryAttributeTemplate
{
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition AttributeDefinition { get; set; } = null!;
    public bool IsRequired { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
}
