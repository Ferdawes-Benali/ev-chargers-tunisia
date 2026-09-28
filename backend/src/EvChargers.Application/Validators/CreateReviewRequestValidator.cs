using FluentValidation;
using EvChargers.Application.DTOs;

namespace EvChargers.Application.Validators;

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public const int MaxCommentLength = 1000;

    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        // Optional: null or empty is fine
        RuleFor(x => x.Comment).MaximumLength(MaxCommentLength);
    }
}