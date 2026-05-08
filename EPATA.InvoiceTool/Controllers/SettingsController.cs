using EPATA.InvoiceTool.Data;
using EPATA.InvoiceTool.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EPATA.InvoiceTool.Controllers;

[ApiController]
[Route("api/config")]
public class SettingsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var cfg = await db.AppConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1)
                  ?? new AppConfig();
        return Ok(cfg);
    }

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] AppConfig incoming)
    {
        incoming.Id = 1;
        var existing = await db.AppConfigs.FindAsync(1);
        if (existing is null)
            db.AppConfigs.Add(incoming);
        else
            db.Entry(existing).CurrentValues.SetValues(incoming);

        await db.SaveChangesAsync();
        return Ok(incoming);
    }
}
