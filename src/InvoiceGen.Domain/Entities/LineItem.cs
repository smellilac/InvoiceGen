namespace InvoiceGen.Domain.Entities;

public class LineItem
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Reference { get; set; }

    public decimal LineTotal => Quantity * UnitCost;
}
