using FluentAssertions;
using EvChargers.Application.DTOs;
using EvChargers.Application.Validators;
using Xunit;

namespace EvChargers.Tests;

public class CreateReviewRequestValidatorTests
{
    private readonly CreateReviewRequestValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Invalid_rating_is_rejected(int rating)
    {
        var result = _validator.Validate(new CreateReviewRequest(rating, "test"));
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Valid_rating_is_accepted(int rating)
    {
        var result = _validator.Validate(new CreateReviewRequest(rating, "test"));
        result.IsValid.Should().BeTrue();
    }
}