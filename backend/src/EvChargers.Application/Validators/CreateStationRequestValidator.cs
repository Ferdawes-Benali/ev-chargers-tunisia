using FluentValidation;
using EvChargers.Application.DTOs;

namespace EvChargers.Application.Validators;

public class CreateStationRequestValidator : AbstractValidator<CreateStationRequest>
{
    public CreateStationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90);
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180);
        RuleFor(x => x.Connectors).NotEmpty();
    }
}