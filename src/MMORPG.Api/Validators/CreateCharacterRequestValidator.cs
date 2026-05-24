using FluentValidation;
using MMORPG.Api.DTOs;

namespace MMORPG.Api.Validators;

public class CreateCharacterRequestValidator : AbstractValidator<CreateCharacterRequest>
{
    // Letters only; each hyphen/apostrophe must be immediately followed by a letter
    // so consecutive specials and trailing specials are rejected automatically.
    private static readonly System.Text.RegularExpressions.Regex NameRegex =
        new(@"^[A-Za-z]([A-Za-z]|[-'][A-Za-z])*$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public CreateCharacterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Length(3, 20).WithMessage("Name must be between 3 and 20 characters.")
            .Must(n => n.Length >= 1 && char.IsLetter(n[0]))
                .WithMessage("Name must start with a letter.")
            .Matches(NameRegex)
                .WithMessage("Name may only contain letters, hyphens, and apostrophes (no consecutive specials).");

        RuleFor(x => x.ClassId)
            .GreaterThan(0).WithMessage("ClassId must be a positive integer.");
    }
}
