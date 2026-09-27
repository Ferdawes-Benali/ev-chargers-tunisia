using FluentValidation;
using EvChargers.Application.DTOs;

namespace EvChargers.Application.Validators;

public class TripPlanRequestValidator : AbstractValidator<TripPlanRequest>
{
    public TripPlanRequestValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.BatteryPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.OriginLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.OriginLng).InclusiveBetween(-180, 180);
        RuleFor(x => x.DestLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.DestLng).InclusiveBetween(-180, 180);
    }
}
