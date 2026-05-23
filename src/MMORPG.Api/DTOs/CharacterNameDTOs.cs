namespace MMORPG.Api.DTOs;

public record NameValidationResponse(bool Available, string? Reason = null);
