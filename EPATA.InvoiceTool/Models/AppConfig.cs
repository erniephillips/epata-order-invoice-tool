namespace EPATA.InvoiceTool.Models;

/// <summary>Singleton row (Id always = 1) for app-wide settings.</summary>
public class AppConfig
{
    public int Id { get; set; } = 1;

    // ── Business info ──────────────────────────────────────────────
    public string? BusinessName { get; set; } = "EPATA 3D PRINTS";
    public string? BusinessLocation { get; set; } = "Based in NJ";
    public string? BusinessEmail { get; set; } = "epata.llc.co@gmail.com";
    public string? BusinessPhone { get; set; } = "(973) 306-8628";
    public string? BusinessWebsite { get; set; } = "https://erniephillipsportfolio.com/";
    public string? BusinessEtsy { get; set; } = "https://www.etsy.com/shop/epata3dprints";
    public string? BusinessInstagram { get; set; } = "@epata3dprints";
    public string? BusinessFacebook { get; set; } = "EPATA 3D Prints";
    public string BrandColor { get; set; } = "#17468f";

    // ── Calculator defaults ────────────────────────────────────────
    public decimal CalcGramRate { get; set; } = 0.05m;
    public decimal CalcHourRate { get; set; } = 3m;
    public decimal CalcDesignRate { get; set; } = 25m;
    public decimal CalcSetupFee { get; set; } = 0m;
    public decimal CalcPostFee { get; set; } = 0m;
    public decimal CalcMinimum { get; set; } = 15m;
}
