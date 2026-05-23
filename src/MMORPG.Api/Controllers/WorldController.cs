using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMORPG.Api.Data;

namespace MMORPG.Api.Controllers;

[ApiController]
[Route("api/world")]
[Authorize]
public class WorldController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("zones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetZones()
    {
        var zones = await db.Zones
            .Select(z => new
            {
                z.Id,
                z.Name,
                z.Description,
                z.ZoneType,
                z.MinLevel,
                z.MaxLevel,
                z.IsPvp,
                z.IsInstanced,
                z.MaxPlayers
            })
            .ToListAsync();

        return Ok(zones);
    }

    [HttpGet("zones/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetZone(int id)
    {
        var zone = await db.Zones
            .Where(z => z.Id == id)
            .Select(z => new
            {
                z.Id,
                z.Name,
                z.Description,
                z.ZoneType,
                z.MinLevel,
                z.MaxLevel,
                z.IsPvp,
                z.IsInstanced,
                z.MaxPlayers,
                OnlinePlayers = z.Characters.Count(c => c.IsOnline)
            })
            .FirstOrDefaultAsync();

        if (zone is null) return NotFound();
        return Ok(zone);
    }
}
