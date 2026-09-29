using FluentValidation;
using EvChargers.Application.Common;
using EvChargers.Application.DTOs;

namespace EvChargers.Application.Validators;

public class UpdateLanguageRequestValidator : AbstractValidator<UpdateLanguageRequest>
{
    public UpdateLanguageRequestValidator() =>
        RuleFor(x => x.Language)
            .Must(Languages.IsSupported)
            .WithMessage("Language must be one of: fr, ar, en.");
}
