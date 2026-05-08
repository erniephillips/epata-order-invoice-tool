namespace EPATA.InvoiceTool.Models;

public class Document
{
    public int Id { get; set; }

    // ── Core ──────────────────────────────────────────────────────
    public string? DocNumber { get; set; }
    public string DocType { get; set; } = "ESTIMATE";   // ESTIMATE | INVOICE
    public string Status { get; set; } = "Draft";       // Draft | Sent | Paid | Void

    // ── Customer ──────────────────────────────────────────────────
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerEmail { get; set; }
    public string? PreparedFor { get; set; }

    // ── Project ───────────────────────────────────────────────────
    public string? ProjectName { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public string? Infill { get; set; }
    public string? ProjectDescription { get; set; }
    public string? ProjectNotes { get; set; }

    // ── Document settings ─────────────────────────────────────────
    public string? PageSize { get; set; }
    public string? DocDate { get; set; }
    public string? DueDate { get; set; }

    // ── Pricing totals ────────────────────────────────────────────
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal RushAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    // ── PDF notes ─────────────────────────────────────────────────
    public string? PricingGuide { get; set; }
    public string? TermsNotes { get; set; }
    public string? StandardTurnaround { get; set; }
    public string? RushTurnaround { get; set; }

    // ── Calculator snapshot ───────────────────────────────────────
    public decimal CalcGrams { get; set; }
    public decimal CalcHours { get; set; }
    public decimal CalcDesignHours { get; set; }
    public decimal CalcSetupFee { get; set; }
    public decimal CalcPostFee { get; set; }
    public decimal CalcGramRate { get; set; } = 0.05m;
    public decimal CalcHourRate { get; set; } = 3m;
    public decimal CalcDesignRate { get; set; } = 25m;
    public decimal CalcMinimum { get; set; } = 15m;
    public decimal CalcDifficulty { get; set; } = 1m;
    public decimal CalcRush { get; set; }
    public decimal CalcDiscount { get; set; }
    public decimal CalcTaxRate { get; set; }

    // ── Legacy JSON (safety net, full form state) ─────────────────
    public string Json { get; set; } = "{}";

    // ── Timestamps ────────────────────────────────────────────────
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    // ── Navigation ────────────────────────────────────────────────
    public List<LineItem> LineItems { get; set; } = [];
}
