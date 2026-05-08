namespace EPATA.InvoiceTool.Models;

public class LineItem
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public string? Details { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }

    public Document? Document { get; set; }
}
