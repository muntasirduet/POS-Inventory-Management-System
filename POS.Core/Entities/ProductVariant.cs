namespace POS.Core.Entities;
public class ProductVariant : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string? Weight { get; set; }
    public decimal Price { get; set; }
    public int StockQty { get; set; }
    public string? SKU { get; set; }
    public string? Barcode { get; set; }
}
