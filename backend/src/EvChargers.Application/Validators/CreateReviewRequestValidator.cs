using FluentValidation;
using EvChargers.Application.DTOs;

namespace EvChargers.Application.Validators;

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator() => RuleFor(x => x.Rating).InclusiveBetween(1, 5);
}