using POS.Core.Enums;
namespace POS.Core.Entities;
public class Payment : BaseEntity
{
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public decimal? Change { get; set; }
    public string? Reference { get; set; }
    public bool IsSuccessful { get; set; } = true;
}
