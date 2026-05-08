using EPATA.InvoiceTool.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace EPATA.InvoiceTool.Controllers;

[ApiController]
[Route("api/database")]
public class DatabaseController(AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private string DbPath => Path.Combine(env.ContentRootPath, "App_Data", "epata_invoices.sqlite");

    // ── GET /api/database/backup ──────────────────────────────────────────
    [HttpGet("backup")]
    public IActionResult Backup()
    {
        if (!System.IO.File.Exists(DbPath))
            return NotFound("Database file not found.");

        return PhysicalFile(DbPath, "application/x-sqlite3", "EPATA_Invoices.sqlite");
    }

    // ── POST /api/database/import ─────────────────────────────────────────
    [HttpPost("import")]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No SQLite file provided.");

        var tempPath = Path.Combine(Path.GetTempPath(), $"epata-import-{Guid.NewGuid():N}.sqlite");
        try
        {
            await using (var stream = System.IO.File.Create(tempPath))
                await file.CopyToAsync(stream);

            // Validate
            ValidateDatabase(tempPath);

            // Replace
            System.IO.File.Copy(tempPath, DbPath, overwrite: true);

            // Re-init schema on new DB
            await db.InitializeSchemaAsync();

            return Ok(new { imported = true });
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        finally
        {
            if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
        }
    }

    // ── POST /api/database/clear ──────────────────────────────────────────
    [HttpPost("clear")]
    public async Task<IActionResult> Clear()
    {
        await db.Database.ExecuteSqlRawAsync("DELETE FROM LineItems;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Documents;");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('LineItems', 'Documents');");

        return Ok(new { cleared = true });
    }

    // ── GET /api/health ───────────────────────────────────────────────────
    [HttpGet("/api/health")]
    public IActionResult Health() => Ok(new
    {
        status      = "ok",
        database    = DbPath,
        serverTimeUtc = DateTimeOffset.UtcNow
    });

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void ValidateDatabase(string path)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode       = SqliteOpenMode.ReadOnly
        }.ToString();

        using var conn = new SqliteConnection(cs);
        conn.Open();
        using var cmd  = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Documents';";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            throw new InvalidOperationException(
                "The uploaded file does not contain a valid EPATA Documents table.");
    }
}
