using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMORPG.Api.DTOs;
using MMORPG.Api.Services;

namespace MMORPG.Api.Controllers;

[ApiController]
[Route("api/characters")]
[Authorize]
public class CharactersController(ICharacterService characterService) : ControllerBase
{
    private Guid PlayerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("validate-name")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(NameValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateName([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new ProblemDetails { Detail = "name query parameter is required." });

        var result = await characterService.ValidateNameAsync(name);
        return Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CharacterSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCharacters()
    {
        var characters = await characterService.GetByPlayerAsync(PlayerId);
        return Ok(characters);
    }

    [HttpGet("{id:guid}/sheet")]
    [ProducesResponseType(typeof(CharacterSheetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSheet(Guid id)
    {
        try
        {
            var sheet = await characterService.GetSheetAsync(id, PlayerId);
            return Ok(sheet);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(CharacterSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCharacterRequest request)
    {
        try
        {
            var character = await characterService.CreateAsync(PlayerId, request);
            return CreatedAtAction(nameof(GetSheet), new { id = character.Id }, character);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Detail = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new ProblemDetails { Detail = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await characterService.DeleteAsync(id, PlayerId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
