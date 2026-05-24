namespace MMORPG.Api.Configuration;

public class NameValidationOptions
{
    public const string SectionName = "NameValidation";

    /// <summary>Names that can never be used by players, regardless of availability.</summary>
    public IReadOnlyList<string> ReservedNames { get; set; } =
    [
        "Admin", "Administrator", "GameMaster", "GM", "Moderator",
        "Mod", "Dev", "Developer", "Support", "Staff", "System",
        "Null", "Undefined", "Unknown", "Anonymous", "Default"
    ];
}
