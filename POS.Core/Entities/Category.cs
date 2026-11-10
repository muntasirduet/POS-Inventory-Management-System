namespace POS.Core.Entities;
public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public string? Description { get; set; }
    /// <summary>Bootstrap icon name, e.g. "bi-bag", shown in POS category picker.</summary>
    public string? IconClass { get; set; }
    /// <summary>Hex color for POS category button, e.g. "#4f46e5".</summary>
    public string? DisplayColor { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsPosVisible { get; set; } = true;
    /// <summary>Default tax group auto-assigned to new products in this category.</summary>
    public int? DefaultTaxGroupId { get; set; }
    public TaxGroup? DefaultTaxGroup { get; set; }
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<PriceRule> PriceRules { get; set; } = new List<PriceRule>();
    public ICollection<CategoryAttributeTemplate> AttributeTemplates { get; set; } = new List<CategoryAttributeTemplate>();
}
