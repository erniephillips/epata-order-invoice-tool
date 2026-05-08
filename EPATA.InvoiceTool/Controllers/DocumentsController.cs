using EPATA.InvoiceTool.Data;
using EPATA.InvoiceTool.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EPATA.InvoiceTool.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController(AppDbContext db) : ControllerBase
{
    // ── GET /api/documents ────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromQuery] string? status)
    {
        var query = db.Documents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(d => d.DocType == type.ToUpper());

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.Status == status);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.ToLower();
            query = query.Where(d =>
                (d.DocNumber   != null && d.DocNumber.ToLower().Contains(term)) ||
                (d.CustomerName != null && d.CustomerName.ToLower().Contains(term)) ||
                (d.ProjectName  != null && d.ProjectName.ToLower().Contains(term)));
        }

        var rows = await query
            .OrderByDescending(d => d.UpdatedAt)
            .ThenByDescending(d => d.Id)
            .Select(d => new
            {
                d.Id, d.DocNumber, d.DocType, d.Status,
                d.CustomerName, d.ProjectName,
                d.Total, d.AmountPaid, d.Balance,
                d.DocDate, d.DueDate,
                d.CreatedAt, d.UpdatedAt
            })
            .ToListAsync();

        return Ok(rows);
    }

    // ── GET /api/documents/stats ──────────────────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var all = await db.Documents.AsNoTracking()
            .Select(d => new { d.DocType, d.Status, d.Total, d.AmountPaid, d.Balance })
            .ToListAsync();

        return Ok(new
        {
            totalEstimates  = all.Count(d => d.DocType == "ESTIMATE"),
            totalInvoices   = all.Count(d => d.DocType == "INVOICE"),
            totalRevenue    = all.Where(d => d.DocType == "INVOICE").Sum(d => d.Total),
            paidRevenue     = all.Where(d => d.DocType == "INVOICE" && d.Status == "Paid").Sum(d => d.Total),
            unpaidBalance   = all.Where(d => d.DocType == "INVOICE" && d.Status != "Paid" && d.Status != "Void").Sum(d => d.Balance),
            draftCount      = all.Count(d => d.Status == "Draft"),
            sentCount       = all.Count(d => d.Status == "Sent"),
            paidCount       = all.Count(d => d.Status == "Paid"),
            voidCount       = all.Count(d => d.Status == "Void"),
        });
    }

    // ── GET /api/documents/latest ─────────────────────────────────────────
    [HttpGet("latest")]
    public async Task<IActionResult> Latest()
    {
        var doc = await db.Documents.AsNoTracking()
            .Include(d => d.LineItems.OrderBy(li => li.SortOrder))
            .OrderByDescending(d => d.UpdatedAt)
            .ThenByDescending(d => d.Id)
            .FirstOrDefaultAsync();

        return doc is null ? NotFound("No saved documents yet.") : Ok(ToDto(doc));
    }

    // ── GET /api/documents/next-number ────────────────────────────────────
    [HttpGet("next-number")]
    public async Task<IActionResult> NextNumber([FromQuery] string type = "ESTIMATE")
    {
        var prefix = type.ToUpper() == "INVOICE" ? "INV" : "EST";
        var year   = DateTime.Now.Year;
        var yearStr = year.ToString();

        var maxNum = await db.Documents.AsNoTracking()
            .Where(d => d.DocNumber != null && d.DocNumber.StartsWith($"{prefix}-{yearStr}-"))
            .Select(d => d.DocNumber!)
            .ToListAsync();

        int next = 1;
        foreach (var n in maxNum)
        {
            var parts = n.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
                next = Math.Max(next, seq + 1);
        }

        return Ok(new { number = $"{prefix}-{year}-{next:D4}" });
    }

    // ── GET /api/documents/{id} ───────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var doc = await db.Documents.AsNoTracking()
            .Include(d => d.LineItems.OrderBy(li => li.SortOrder))
            .FirstOrDefaultAsync(d => d.Id == id);

        return doc is null ? NotFound($"Document {id} not found.") : Ok(ToDto(doc));
    }

    // ── POST /api/documents ───────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveDocumentRequest req)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var doc = new Document { CreatedAt = now, UpdatedAt = now };
        ApplyRequest(doc, req);

        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        return Ok(new { id = doc.Id });
    }

    // ── PUT /api/documents/{id} ───────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveDocumentRequest req)
    {
        var doc = await db.Documents
            .Include(d => d.LineItems)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (doc is null) return NotFound($"Document {id} not found.");

        doc.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        ApplyRequest(doc, req);

        await db.SaveChangesAsync();
        return Ok(new { id = doc.Id });
    }

    // ── DELETE /api/documents/{id} ────────────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var doc = await db.Documents.FindAsync(id);
        if (doc is null) return NotFound($"Document {id} not found.");

        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        return Ok(new { deleted = id });
    }

    // ── POST /api/documents/{id}/duplicate ────────────────────────────────
    [HttpPost("{id:int}/duplicate")]
    public async Task<IActionResult> Duplicate(int id)
    {
        var src = await db.Documents.AsNoTracking()
            .Include(d => d.LineItems.OrderBy(li => li.SortOrder))
            .FirstOrDefaultAsync(d => d.Id == id);

        if (src is null) return NotFound($"Document {id} not found.");

        // Get next document number
        var prefix = src.DocType == "INVOICE" ? "INV" : "EST";
        var year   = DateTime.Now.Year;
        var yearStr = year.ToString();
        var maxNums = await db.Documents.AsNoTracking()
            .Where(d => d.DocNumber != null && d.DocNumber.StartsWith($"{prefix}-{yearStr}-"))
            .Select(d => d.DocNumber!).ToListAsync();
        int next = 1;
        foreach (var n in maxNums)
        {
            var parts = n.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
                next = Math.Max(next, seq + 1);
        }

        var now = DateTimeOffset.UtcNow.ToString("O");
        var copy = new Document
        {
            DocNumber = $"{prefix}-{year}-{next:D4}",
            DocType = src.DocType, Status = "Draft",
            CustomerName = src.CustomerName, CustomerPhone = src.CustomerPhone,
            CustomerAddress = src.CustomerAddress, CustomerEmail = src.CustomerEmail,
            PreparedFor = src.PreparedFor, ProjectName = src.ProjectName,
            Material = src.Material, Color = src.Color, Infill = src.Infill,
            ProjectDescription = src.ProjectDescription, ProjectNotes = src.ProjectNotes,
            PageSize = src.PageSize,
            DocDate = DateTime.Now.ToString("yyyy-MM-dd"),
            DueDate = DateTime.Now.AddDays(src.DocType == "INVOICE" ? 7 : 14).ToString("yyyy-MM-dd"),
            Subtotal = src.Subtotal, DiscountAmount = src.DiscountAmount,
            RushAmount = src.RushAmount, TaxAmount = src.TaxAmount,
            Total = src.Total, AmountPaid = 0, Balance = src.Total,
            PricingGuide = src.PricingGuide, TermsNotes = src.TermsNotes,
            StandardTurnaround = src.StandardTurnaround, RushTurnaround = src.RushTurnaround,
            CalcGrams = src.CalcGrams, CalcHours = src.CalcHours,
            CalcDesignHours = src.CalcDesignHours, CalcSetupFee = src.CalcSetupFee,
            CalcPostFee = src.CalcPostFee, CalcGramRate = src.CalcGramRate,
            CalcHourRate = src.CalcHourRate, CalcDesignRate = src.CalcDesignRate,
            CalcMinimum = src.CalcMinimum, CalcDifficulty = src.CalcDifficulty,
            CalcRush = src.CalcRush, CalcDiscount = src.CalcDiscount,
            CalcTaxRate = src.CalcTaxRate, Json = src.Json,
            CreatedAt = now, UpdatedAt = now,
            LineItems = src.LineItems.Select(li => new LineItem
            {
                SortOrder = li.SortOrder, Description = li.Description,
                Details = li.Details, Quantity = li.Quantity,
                Rate = li.Rate, Amount = li.Amount
            }).ToList()
        };

        db.Documents.Add(copy);
        await db.SaveChangesAsync();
        return Ok(ToDto(copy));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void ApplyRequest(Document doc, SaveDocumentRequest r)
    {
        doc.DocNumber = r.DocNumber; doc.DocType = r.DocType ?? "ESTIMATE";
        doc.Status = r.Status ?? "Draft";
        doc.CustomerName = r.CustomerName; doc.CustomerPhone = r.CustomerPhone;
        doc.CustomerAddress = r.CustomerAddress; doc.CustomerEmail = r.CustomerEmail;
        doc.PreparedFor = r.PreparedFor; doc.ProjectName = r.ProjectName;
        doc.Material = r.Material; doc.Color = r.Color; doc.Infill = r.Infill;
        doc.ProjectDescription = r.ProjectDescription; doc.ProjectNotes = r.ProjectNotes;
        doc.PageSize = r.PageSize; doc.DocDate = r.DocDate; doc.DueDate = r.DueDate;
        doc.Subtotal = r.Subtotal; doc.DiscountAmount = r.DiscountAmount;
        doc.RushAmount = r.RushAmount; doc.TaxAmount = r.TaxAmount;
        doc.Total = r.Total; doc.AmountPaid = r.AmountPaid; doc.Balance = r.Balance;
        doc.PricingGuide = r.PricingGuide; doc.TermsNotes = r.TermsNotes;
        doc.StandardTurnaround = r.StandardTurnaround; doc.RushTurnaround = r.RushTurnaround;
        doc.CalcGrams = r.CalcGrams; doc.CalcHours = r.CalcHours;
        doc.CalcDesignHours = r.CalcDesignHours; doc.CalcSetupFee = r.CalcSetupFee;
        doc.CalcPostFee = r.CalcPostFee; doc.CalcGramRate = r.CalcGramRate;
        doc.CalcHourRate = r.CalcHourRate; doc.CalcDesignRate = r.CalcDesignRate;
        doc.CalcMinimum = r.CalcMinimum; doc.CalcDifficulty = r.CalcDifficulty;
        doc.CalcRush = r.CalcRush; doc.CalcDiscount = r.CalcDiscount;
        doc.CalcTaxRate = r.CalcTaxRate; doc.Json = r.Json ?? "{}";

        // Sync line items (full replace)
        doc.LineItems.Clear();
        if (r.LineItems is { Count: > 0 })
        {
            doc.LineItems.AddRange(r.LineItems.Select((li, i) => new LineItem
            {
                SortOrder   = li.SortOrder > 0 ? li.SortOrder : i,
                Description = li.Description,
                Details     = li.Details,
                Quantity    = li.Quantity,
                Rate        = li.Rate,
                Amount      = li.Amount
            }));
        }
    }

    private static object ToDto(Document d) => new
    {
        d.Id, d.DocNumber, d.DocType, d.Status,
        d.CustomerName, d.CustomerPhone, d.CustomerAddress, d.CustomerEmail, d.PreparedFor,
        d.ProjectName, d.Material, d.Color, d.Infill, d.ProjectDescription, d.ProjectNotes,
        d.PageSize, d.DocDate, d.DueDate,
        d.Subtotal, d.DiscountAmount, d.RushAmount, d.TaxAmount,
        d.Total, d.AmountPaid, d.Balance,
        d.PricingGuide, d.TermsNotes, d.StandardTurnaround, d.RushTurnaround,
        d.CalcGrams, d.CalcHours, d.CalcDesignHours, d.CalcSetupFee, d.CalcPostFee,
        d.CalcGramRate, d.CalcHourRate, d.CalcDesignRate, d.CalcMinimum,
        d.CalcDifficulty, d.CalcRush, d.CalcDiscount, d.CalcTaxRate,
        d.Json, d.CreatedAt, d.UpdatedAt,
        lineItems = d.LineItems.OrderBy(li => li.SortOrder).Select(li => new
        {
            li.Id, li.SortOrder, li.Description, li.Details,
            li.Quantity, li.Rate, li.Amount
        })
    };
}

// ── Request DTOs ─────────────────────────────────────────────────────────────

public sealed record SaveDocumentRequest(
    string? DocNumber, string? DocType, string? Status,
    string? CustomerName, string? CustomerPhone, string? CustomerAddress,
    string? CustomerEmail, string? PreparedFor,
    string? ProjectName, string? Material, string? Color, string? Infill,
    string? ProjectDescription, string? ProjectNotes,
    string? PageSize, string? DocDate, string? DueDate,
    decimal Subtotal, decimal DiscountAmount, decimal RushAmount,
    decimal TaxAmount, decimal Total, decimal AmountPaid, decimal Balance,
    string? PricingGuide, string? TermsNotes,
    string? StandardTurnaround, string? RushTurnaround,
    decimal CalcGrams, decimal CalcHours, decimal CalcDesignHours,
    decimal CalcSetupFee, decimal CalcPostFee,
    decimal CalcGramRate, decimal CalcHourRate, decimal CalcDesignRate,
    decimal CalcMinimum, decimal CalcDifficulty,
    decimal CalcRush, decimal CalcDiscount, decimal CalcTaxRate,
    List<LineItemRequest>? LineItems,
    string? Json);

public sealed record LineItemRequest(
    int? Id, int SortOrder,
    string? Description, string? Details,
    decimal Quantity, decimal Rate, decimal Amount);
