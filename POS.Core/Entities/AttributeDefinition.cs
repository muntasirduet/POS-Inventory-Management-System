namespace POS.Core.Entities;

public class AttributeDefinition : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Text | Number | Boolean | Select</summary>
    public string DataType { get; set; } = "Text";
    /// <summary>True = shows per variant (Size, Color). False = shows on the product itself (Brand, Origin).</summary>
    public bool IsVariantLevel { get; set; } = true;
    public bool IsFilterable { get; set; } = false;
    public int SortOrder { get; set; } = 0;

    public ICollection<AttributeOption> Options { get; set; } = new List<AttributeOption>();
    public ICollection<CategoryAttributeTemplate> CategoryTemplates { get; set; } = new List<CategoryAttributeTemplate>();
}
