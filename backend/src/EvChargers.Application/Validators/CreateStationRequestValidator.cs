using FluentValidation;
using EvChargers.Application.DTOs;
using EvChargers.Domain.Enums;

namespace EvChargers.Application.Validators;

public class CreateStationRequestValidator : AbstractValidator<CreateStationRequest>
{
    public CreateStationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90);
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180);
        RuleFor(x => x.Connectors).NotEmpty();
        RuleForEach(x => x.Connectors)
            .ChildRules(c =>
            {
                c.RuleFor(x => x.Type)
                    .Must(t => Enum.TryParse<ConnectorType>(t, true, out _))
                    .WithMessage("Connector type must be one of: Type2, CCS, CHAdeMO, Tesla.");
                c.RuleFor(x => x.PowerKw).GreaterThan(0);
            });
    }
}