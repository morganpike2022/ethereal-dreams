using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MMORPG.Api.Data;
using MMORPG.Api.DTOs;
using MMORPG.Api.Models;

namespace MMORPG.Api.Controllers;

[ApiController]
[Route("api/guilds")]
[Authorize]
public class GuildsController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GuildDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGuilds([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Guilds.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g => g.Name.Contains(search));

        var guilds = await query
            .OrderByDescending(g => g.Level)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GuildDto(
                g.Id, g.Name, g.Tag, g.Description,
                g.Level, g.Motd,
                g.Members.Count,
                g.CreatedAt
            ))
            .ToListAsync();

        return Ok(guilds);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GuildDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGuild(Guid id)
    {
        var guild = await db.Guilds
            .Include(g => g.Members)
            .Where(g => g.Id == id)
            .Select(g => new GuildDto(
                g.Id, g.Name, g.Tag, g.Description,
                g.Level, g.Motd,
                g.Members.Count,
                g.CreatedAt
            ))
            .FirstOrDefaultAsync();

        if (guild is null) return NotFound();
        return Ok(guild);
    }

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<GuildMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var members = await db.GuildMembers
            .Where(m => m.GuildId == id)
            .Include(m => m.Character).ThenInclude(c => c.Class)
            .Select(m => new GuildMemberDto(
                m.CharacterId,
                m.Character.Name,
                m.Character.Class.DisplayName,
                m.Character.Level,
                m.Rank,
                m.ContributionXp,
                m.JoinedAt,
                m.LastActiveAt
            ))
            .ToListAsync();

        return Ok(members);
    }

    [HttpPost]
    [ProducesResponseType(typeof(GuildDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateGuild([FromBody] CreateGuildRequest request)
    {
        if (await db.Guilds.AnyAsync(g => g.Name == request.Name))
            return Conflict(new ProblemDetails { Detail = "Guild name already taken." });

        if (await db.Guilds.AnyAsync(g => g.Tag == request.Tag.ToUpper()))
            return Conflict(new ProblemDetails { Detail = "Guild tag already taken." });

        var guild = new Guild
        {
            Id          = Guid.NewGuid(),
            Name        = request.Name,
            Tag         = request.Tag.ToUpper(),
            Description = request.Description,
            CreatedAt   = DateTimeOffset.UtcNow,
            UpdatedAt   = DateTimeOffset.UtcNow
        };

        db.Guilds.Add(guild);
        await db.SaveChangesAsync();

        var dto = new GuildDto(guild.Id, guild.Name, guild.Tag, guild.Description, guild.Level, guild.Motd, 0, guild.CreatedAt);
        return CreatedAtAction(nameof(GetGuild), new { id = guild.Id }, dto);
    }
}
