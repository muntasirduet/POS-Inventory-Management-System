namespace POS.Application.DTOs;
public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int? TaxRateId { get; set; }
    public string? TaxRateName { get; set; }
    public decimal? TaxRateValue { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public int StockQty { get; set; }
}
