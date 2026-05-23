using FluentValidation;
using MMORPG.Api.DTOs;

namespace MMORPG.Api.Validators;

public class CreateCharacterRequestValidator : AbstractValidator<CreateCharacterRequest>
{
    private static readonly System.Text.RegularExpressions.Regex NameRegex =
        new(@"^[A-Za-z][A-Za-z0-9\-']{1,31}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public CreateCharacterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Length(2, 32).WithMessage("Name must be between 2 and 32 characters.")
            .Must(n => n.Length >= 2 && char.IsLetter(n[0]))
                .WithMessage("Name must start with a letter.")
            .Matches(NameRegex)
                .WithMessage("Name may only contain letters, digits, hyphens, and apostrophes.");

        RuleFor(x => x.ClassId)
            .GreaterThan(0).WithMessage("ClassId must be a positive integer.");
    }
}
