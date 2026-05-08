using System.Text.Json;
using EPATA.InvoiceTool.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EPATA.InvoiceTool.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document>  Documents  => Set<Document>();
    public DbSet<LineItem>  LineItems  => Set<LineItem>();
    public DbSet<AppConfig> AppConfigs => Set<AppConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("Documents");
            e.HasKey(d => d.Id);
            e.Property(d => d.DocType).HasDefaultValue("ESTIMATE");
            e.Property(d => d.Status).HasDefaultValue("Draft");
            e.Property(d => d.Json).HasDefaultValue("{}");
            e.HasIndex(d => d.DocNumber);
            e.HasIndex(d => d.UpdatedAt);
            e.HasMany(d => d.LineItems)
             .WithOne(li => li.Document)
             .HasForeignKey(li => li.DocumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LineItem>(e =>
        {
            e.ToTable("LineItems");
            e.HasKey(li => li.Id);
        });

        modelBuilder.Entity<AppConfig>(e =>
        {
            e.ToTable("AppConfig");
            e.HasKey(c => c.Id);
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Schema bootstrap — safe to call on both fresh installs and upgrades.
    //  Uses SqliteConnection directly so AddWithValue is available on all
    //  command objects (DbParameterCollection lacks it).
    // ─────────────────────────────────────────────────────────────────────
    public async Task InitializeSchemaAsync()
    {
        // Get the connection string from EF Core options and open our own
        // SqliteConnection so every CreateCommand() returns SqliteCommand.
        var cs = Database.GetConnectionString()
                 ?? throw new InvalidOperationException("No SQLite connection string found.");

        using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        await CreateDocumentsTableAsync(conn);
        await AddMissingColumnsAsync(conn);
        await CreateLineItemsTableAsync(conn);
        await CreateAppConfigTableAsync(conn);
        await BackfillLegacyJsonAsync(conn);
    }

    // ── Table creation ────────────────────────────────────────────────────

    private static async Task CreateDocumentsTableAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Documents (
                Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                DocNumber          TEXT    NULL,
                DocType            TEXT    NOT NULL DEFAULT 'ESTIMATE',
                Status             TEXT    NOT NULL DEFAULT 'Draft',
                CustomerName       TEXT    NULL,
                CustomerPhone      TEXT    NULL,
                CustomerAddress    TEXT    NULL,
                CustomerEmail      TEXT    NULL,
                PreparedFor        TEXT    NULL,
                ProjectName        TEXT    NULL,
                Material           TEXT    NULL,
                Color              TEXT    NULL,
                Infill             TEXT    NULL,
                ProjectDescription TEXT    NULL,
                ProjectNotes       TEXT    NULL,
                PageSize           TEXT    NULL,
                DocDate            TEXT    NULL,
                DueDate            TEXT    NULL,
                Subtotal           REAL    NOT NULL DEFAULT 0,
                DiscountAmount     REAL    NOT NULL DEFAULT 0,
                RushAmount         REAL    NOT NULL DEFAULT 0,
                TaxAmount          REAL    NOT NULL DEFAULT 0,
                Total              REAL    NOT NULL DEFAULT 0,
                AmountPaid         REAL    NOT NULL DEFAULT 0,
                Balance            REAL    NOT NULL DEFAULT 0,
                PricingGuide       TEXT    NULL,
                TermsNotes         TEXT    NULL,
                StandardTurnaround TEXT    NULL,
                RushTurnaround     TEXT    NULL,
                CalcGrams          REAL    NOT NULL DEFAULT 0,
                CalcHours          REAL    NOT NULL DEFAULT 0,
                CalcDesignHours    REAL    NOT NULL DEFAULT 0,
                CalcSetupFee       REAL    NOT NULL DEFAULT 0,
                CalcPostFee        REAL    NOT NULL DEFAULT 0,
                CalcGramRate       REAL    NOT NULL DEFAULT 0.05,
                CalcHourRate       REAL    NOT NULL DEFAULT 3,
                CalcDesignRate     REAL    NOT NULL DEFAULT 25,
                CalcMinimum        REAL    NOT NULL DEFAULT 15,
                CalcDifficulty     REAL    NOT NULL DEFAULT 1,
                CalcRush           REAL    NOT NULL DEFAULT 0,
                CalcDiscount       REAL    NOT NULL DEFAULT 0,
                CalcTaxRate        REAL    NOT NULL DEFAULT 0,
                Json               TEXT    NOT NULL DEFAULT '{}',
                CreatedAt          TEXT    NOT NULL DEFAULT (datetime('now')),
                UpdatedAt          TEXT    NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS IX_Documents_DocNumber ON Documents (DocNumber);
            CREATE INDEX IF NOT EXISTS IX_Documents_UpdatedAt ON Documents (UpdatedAt DESC);
            CREATE INDEX IF NOT EXISTS IX_Documents_DocType   ON Documents (DocType);
            CREATE INDEX IF NOT EXISTS IX_Documents_Status    ON Documents (Status);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task AddMissingColumnsAsync(SqliteConnection conn)
    {
        // Read existing columns via PRAGMA
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(Documents);";
            using var r = await pragma.ExecuteReaderAsync();
            while (await r.ReadAsync()) existing.Add(r.GetString(1));
        }

        // Columns added in the v2 schema that older databases may lack
        var newCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Status"]             = "TEXT NOT NULL DEFAULT 'Draft'",
            ["CustomerPhone"]      = "TEXT NULL",
            ["CustomerAddress"]    = "TEXT NULL",
            ["CustomerEmail"]      = "TEXT NULL",
            ["PreparedFor"]        = "TEXT NULL",
            ["Material"]           = "TEXT NULL",
            ["Color"]              = "TEXT NULL",
            ["Infill"]             = "TEXT NULL",
            ["ProjectDescription"] = "TEXT NULL",
            ["ProjectNotes"]       = "TEXT NULL",
            ["PageSize"]           = "TEXT NULL",
            ["DocDate"]            = "TEXT NULL",
            ["DueDate"]            = "TEXT NULL",
            ["Subtotal"]           = "REAL NOT NULL DEFAULT 0",
            ["DiscountAmount"]     = "REAL NOT NULL DEFAULT 0",
            ["RushAmount"]         = "REAL NOT NULL DEFAULT 0",
            ["TaxAmount"]          = "REAL NOT NULL DEFAULT 0",
            ["Total"]              = "REAL NOT NULL DEFAULT 0",
            ["AmountPaid"]         = "REAL NOT NULL DEFAULT 0",
            ["Balance"]            = "REAL NOT NULL DEFAULT 0",
            ["PricingGuide"]       = "TEXT NULL",
            ["TermsNotes"]         = "TEXT NULL",
            ["StandardTurnaround"] = "TEXT NULL",
            ["RushTurnaround"]     = "TEXT NULL",
            ["CalcGrams"]          = "REAL NOT NULL DEFAULT 0",
            ["CalcHours"]          = "REAL NOT NULL DEFAULT 0",
            ["CalcDesignHours"]    = "REAL NOT NULL DEFAULT 0",
            ["CalcSetupFee"]       = "REAL NOT NULL DEFAULT 0",
            ["CalcPostFee"]        = "REAL NOT NULL DEFAULT 0",
            ["CalcGramRate"]       = "REAL NOT NULL DEFAULT 0.05",
            ["CalcHourRate"]       = "REAL NOT NULL DEFAULT 3",
            ["CalcDesignRate"]     = "REAL NOT NULL DEFAULT 25",
            ["CalcMinimum"]        = "REAL NOT NULL DEFAULT 15",
            ["CalcDifficulty"]     = "REAL NOT NULL DEFAULT 1",
            ["CalcRush"]           = "REAL NOT NULL DEFAULT 0",
            ["CalcDiscount"]       = "REAL NOT NULL DEFAULT 0",
            ["CalcTaxRate"]        = "REAL NOT NULL DEFAULT 0",
        };

        foreach (var (col, def) in newCols)
        {
            if (!existing.Contains(col))
            {
                using var alter = conn.CreateCommand();
                alter.CommandText = $"ALTER TABLE Documents ADD COLUMN {col} {def};";
                await alter.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task CreateLineItemsTableAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS LineItems (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                DocumentId  INTEGER NOT NULL,
                SortOrder   INTEGER NOT NULL DEFAULT 0,
                Description TEXT    NULL,
                Details     TEXT    NULL,
                Quantity    REAL    NOT NULL DEFAULT 1,
                Rate        REAL    NOT NULL DEFAULT 0,
                Amount      REAL    NOT NULL DEFAULT 0,
                FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_LineItems_DocumentId ON LineItems (DocumentId);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CreateAppConfigTableAsync(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS AppConfig (
                Id                INTEGER PRIMARY KEY,
                BusinessName      TEXT NULL DEFAULT 'EPATA 3D PRINTS',
                BusinessLocation  TEXT NULL DEFAULT 'Based in NJ',
                BusinessEmail     TEXT NULL DEFAULT 'epata.llc.co@gmail.com',
                BusinessPhone     TEXT NULL DEFAULT '(973) 306-8628',
                BusinessWebsite   TEXT NULL DEFAULT 'https://erniephillipsportfolio.com/',
                BusinessEtsy      TEXT NULL DEFAULT 'https://www.etsy.com/shop/epata3dprints',
                BusinessInstagram TEXT NULL DEFAULT '@epata3dprints',
                BusinessFacebook  TEXT NULL DEFAULT 'EPATA 3D Prints',
                BrandColor        TEXT NOT NULL DEFAULT '#17468f',
                CalcGramRate      REAL NOT NULL DEFAULT 0.05,
                CalcHourRate      REAL NOT NULL DEFAULT 3,
                CalcDesignRate    REAL NOT NULL DEFAULT 25,
                CalcSetupFee      REAL NOT NULL DEFAULT 0,
                CalcPostFee       REAL NOT NULL DEFAULT 0,
                CalcMinimum       REAL NOT NULL DEFAULT 15
            );

            INSERT OR IGNORE INTO AppConfig
                (Id, BusinessName, BusinessLocation, BusinessEmail, BusinessPhone, BusinessWebsite, BusinessEtsy, BusinessInstagram, BusinessFacebook, BrandColor)
            VALUES
                (1, 'EPATA 3D PRINTS', 'Based in NJ', 'epata.llc.co@gmail.com', '(973) 306-8628', 'https://erniephillipsportfolio.com/', 'https://www.etsy.com/shop/epata3dprints', '@epata3dprints', 'EPATA 3D Prints', '#17468f');

            UPDATE AppConfig SET
                BusinessName      = COALESCE(NULLIF(BusinessName, ''), 'EPATA 3D PRINTS'),
                BusinessLocation  = COALESCE(NULLIF(BusinessLocation, ''), 'Based in NJ'),
                BusinessEmail     = COALESCE(NULLIF(BusinessEmail, ''), 'epata.llc.co@gmail.com'),
                BusinessPhone     = COALESCE(NULLIF(BusinessPhone, ''), '(973) 306-8628'),
                BusinessWebsite   = COALESCE(NULLIF(BusinessWebsite, ''), 'https://erniephillipsportfolio.com/'),
                BusinessEtsy      = COALESCE(NULLIF(BusinessEtsy, ''), 'https://www.etsy.com/shop/epata3dprints'),
                BusinessInstagram = COALESCE(NULLIF(BusinessInstagram, ''), '@epata3dprints'),
                BusinessFacebook  = COALESCE(NULLIF(BusinessFacebook, ''), 'EPATA 3D Prints'),
                BrandColor        = COALESCE(NULLIF(BrandColor, ''), '#17468f')
            WHERE Id = 1;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Legacy JSON backfill ──────────────────────────────────────────────

    private static async Task BackfillLegacyJsonAsync(SqliteConnection conn)
    {
        var todo = new List<(int Id, string Json)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT Id, Json FROM Documents
                WHERE Json IS NOT NULL
                  AND Json != '' AND Json != '{}'
                  AND DocDate IS NULL
                ORDER BY Id;
                """;
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                todo.Add((r.GetInt32(0), r.IsDBNull(1) ? "{}" : r.GetString(1)));
        }

        foreach (var (id, json) in todo)
        {
            try   { await BackfillDocumentAsync(conn, id, json); }
            catch (Exception ex)
            { Console.Error.WriteLine($"[EPATA] Backfill warning #{id}: {ex.Message}"); }
        }
    }

    private static async Task BackfillDocumentAsync(SqliteConnection conn, int id, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("formValues", out var fv)) return;
        root.TryGetProperty("calculatorValues", out var cv);

        string? Fv(string k) => fv.TryGetProperty(k, out var p) ? p.GetString() : null;
        double Cv(string k)
        {
            if (cv.ValueKind != JsonValueKind.Object || !cv.TryGetProperty(k, out var p)) return 0;
            return p.ValueKind switch
            {
                JsonValueKind.Number => p.GetDouble(),
                JsonValueKind.String when double.TryParse(p.GetString(), out var d) => d,
                _ => 0
            };
        }

        // Backfill line items only if none exist yet for this document
        using (var chk = conn.CreateCommand())
        {
            chk.CommandText = "SELECT COUNT(*) FROM LineItems WHERE DocumentId = $id;";
            chk.Parameters.AddWithValue("$id", id);

            if (Convert.ToInt32(await chk.ExecuteScalarAsync()) == 0
                && root.TryGetProperty("lineItems", out var items))
            {
                int ord = 0;
                foreach (var item in items.EnumerateArray())
                {
                    string? ItemStr(string k) => item.TryGetProperty(k, out var p) ? p.GetString() : null;
                    double ItemDbl(string k)
                    {
                        if (!item.TryGetProperty(k, out var p)) return 0;
                        return p.ValueKind switch
                        {
                            JsonValueKind.Number => p.GetDouble(),
                            JsonValueKind.String when double.TryParse(p.GetString(), out var d) => d,
                            _ => 0
                        };
                    }

                    var qty  = ItemDbl("qty");
                    var rate = ItemDbl("rate");

                    using var ins = conn.CreateCommand();
                    ins.CommandText = """
                        INSERT INTO LineItems
                            (DocumentId, SortOrder, Description, Details, Quantity, Rate, Amount)
                        VALUES ($d, $o, $de, $dt, $q, $r, $a);
                        """;
                    ins.Parameters.AddWithValue("$d",  id);
                    ins.Parameters.AddWithValue("$o",  ord++);
                    ins.Parameters.AddWithValue("$de", (object?)ItemStr("desc")    ?? DBNull.Value);
                    ins.Parameters.AddWithValue("$dt", (object?)ItemStr("details") ?? DBNull.Value);
                    ins.Parameters.AddWithValue("$q",  qty);
                    ins.Parameters.AddWithValue("$r",  rate);
                    ins.Parameters.AddWithValue("$a",  qty * rate);
                    await ins.ExecuteNonQueryAsync();
                }
            }
        }

        // Backfill the normalized document columns from form JSON values
        using var upd = conn.CreateCommand();
        upd.CommandText = """
            UPDATE Documents SET
                PreparedFor=$pf,        CustomerPhone=$cp,
                CustomerAddress=$ca,    CustomerEmail=$ce,
                Material=$mat,          Color=$col,
                Infill=$inf,            ProjectDescription=$pd,
                ProjectNotes=$pn,       PageSize=$ps,
                DocDate=$dd,            DueDate=$du,
                PricingGuide=$pg,       TermsNotes=$tn,
                StandardTurnaround=$st, RushTurnaround=$rt,
                CalcGrams=$cg,          CalcHours=$ch,
                CalcDesignHours=$cdh,   CalcSetupFee=$csf,
                CalcPostFee=$cpf,       CalcGramRate=$cgr,
                CalcHourRate=$chr,      CalcDesignRate=$cdr,
                CalcMinimum=$cm,        CalcDifficulty=$cdi,
                CalcRush=$cr,           CalcDiscount=$cdc,
                CalcTaxRate=$ctr
            WHERE Id = $id;
            """;

        void P(string n, object? v) => upd.Parameters.AddWithValue(n, v ?? DBNull.Value);

        P("$pf",  Fv("preparedFor"));
        P("$cp",  Fv("customerPhone"));
        P("$ca",  Fv("customerAddress"));
        P("$ce",  Fv("customerEmail"));
        P("$mat", Fv("material"));
        P("$col", Fv("color"));
        P("$inf", Fv("infill"));
        P("$pd",  Fv("projectDescription"));
        P("$pn",  Fv("projectNotes"));
        P("$ps",  Fv("pageSize"));
        P("$dd",  Fv("docDate"));
        P("$du",  Fv("validUntil"));
        P("$pg",  Fv("pricingGuide"));
        P("$tn",  Fv("termsNotes"));
        P("$st",  Fv("standardTurnaround"));
        P("$rt",  Fv("rushTurnaround"));

        upd.Parameters.AddWithValue("$cg",  Cv("grams"));
        upd.Parameters.AddWithValue("$ch",  Cv("hours"));
        upd.Parameters.AddWithValue("$cdh", Cv("designHours"));
        upd.Parameters.AddWithValue("$csf", Cv("setupFee"));
        upd.Parameters.AddWithValue("$cpf", Cv("postFee"));
        upd.Parameters.AddWithValue("$cgr", Cv("gramRate") is 0 ? 0.05 : Cv("gramRate"));
        upd.Parameters.AddWithValue("$chr", Cv("hourRate") is 0 ? 3    : Cv("hourRate"));
        upd.Parameters.AddWithValue("$cdr", Cv("designRate") is 0 ? 25 : Cv("designRate"));
        upd.Parameters.AddWithValue("$cm",  Cv("minimum") is 0 ? 15    : Cv("minimum"));
        upd.Parameters.AddWithValue("$cdi", Cv("difficulty") is 0 ? 1  : Cv("difficulty"));
        upd.Parameters.AddWithValue("$cr",  Cv("rush"));
        upd.Parameters.AddWithValue("$cdc", Cv("discount"));
        upd.Parameters.AddWithValue("$ctr", Cv("taxRate"));
        upd.Parameters.AddWithValue("$id",  id);

        await upd.ExecuteNonQueryAsync();
    }
}
