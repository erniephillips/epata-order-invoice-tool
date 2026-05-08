using System.Diagnostics;
using EPATA.InvoiceTool.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string appUrl = "http://127.0.0.1:5057";
builder.WebHost.UseUrls(appUrl);

builder.Services.AddControllers();

var appDataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appDataDir);
var dbPath = Path.Combine(appDataDir, "epata_invoices.sqlite");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();

// Bootstrap / migrate schema
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.InitializeSchemaAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// Safety net: for local/single-user use, make sure the schema exists before API calls.
// This prevents "no such table: Documents" when a blank/stale SQLite file is present.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.InitializeSchemaAsync();
    }

    await next();
});

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Lifetime.ApplicationStarted.Register(() =>
{
    try { Process.Start(new ProcessStartInfo { FileName = appUrl, UseShellExecute = true }); }
    catch { /* browser auto-open unavailable — navigate manually */ }
});

Console.WriteLine();
Console.WriteLine("  ╔════════════════════════════════════════════╗");
Console.WriteLine("  ║   EPATA Invoice Tool  ·  .NET 10 SPA       ║");
Console.WriteLine($"  ║   ➜  {appUrl,-38}║");
Console.WriteLine($"  ║   DB: {Path.GetFileName(dbPath),-37}║");
Console.WriteLine("  ║   Ctrl+C to stop                           ║");
Console.WriteLine("  ╚════════════════════════════════════════════╝");
Console.WriteLine();

app.Run();
