using FluentValidation;
using EvChargers.Application.DTOs;
using EvChargers.Domain.Enums;

namespace EvChargers.Application.Validators;

public class CheckinRequestValidator : AbstractValidator<CheckinRequest>
{
    public CheckinRequestValidator()
    {
        RuleFor(x => x.State)
            .Must(s => Enum.TryParse<CheckinState>(s, true, out _))
            .WithMessage("State must be one of: Working, Broken, Occupied.");
    }
}