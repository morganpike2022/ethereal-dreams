using MMORPG.Api.DTOs;

namespace MMORPG.Api.Services;

public interface ICharacterService
{
    Task<IReadOnlyList<CharacterSummaryDto>> GetByPlayerAsync(Guid playerId);
    Task<IReadOnlyList<CharacterSelectDto>> GetSelectScreenAsync(Guid playerId, bool forceRefresh = false);
    Task<CharacterSheetDto> GetSheetAsync(Guid characterId, Guid playerId);
    Task<CharacterSummaryDto> CreateAsync(Guid playerId, CreateCharacterRequest request);
    Task DeleteAsync(Guid characterId, Guid playerId);
    Task<NameValidationResponse> ValidateNameAsync(string name);
}
