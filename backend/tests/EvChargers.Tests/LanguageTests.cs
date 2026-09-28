using System.Security.Claims;
using FluentAssertions;
using EvChargers.API.Extensions;
using EvChargers.Application.DTOs;
using EvChargers.Application.Validators;
using Xunit;

namespace EvChargers.Tests;

public class LanguageTests
{
    // --- Accept-Language → first supported language, else French ---

    [Theory]
    [InlineData("fr-FR", "fr")]
    [InlineData("ar-TN", "ar")]
    [InlineData("en-US,en;q=0.9", "en")]
    [InlineData("de-DE", "fr")]
    [InlineData("", "fr")]
    [InlineData(null, "fr")]
    [InlineData("de-DE,ar-TN;q=0.8,fr;q=0.5", "ar")]
    [InlineData("fr;q=0.3,en;q=0.9", "en")]
    [InlineData("*", "fr")]
    public void Accept_language_picks_first_supported(string? header, string expected)
    {
        AcceptLanguage.PickSupported(header).Should().Be(expected);
    }

    // --- PUT /me/language body ---

    [Theory]
    [InlineData("fr")]
    [InlineData("ar")]
    [InlineData("en")]
    public void Supported_language_is_accepted(string language)
    {
        new UpdateLanguageRequestValidator().Validate(new UpdateLanguageRequest(language)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("de")]
    [InlineData("FR")]
    [InlineData("fr-FR")]
    [InlineData("")]
    [InlineData(null)]
    public void Unsupported_language_is_rejected(string? language)
    {
        new UpdateLanguageRequestValidator().Validate(new UpdateLanguageRequest(language)).IsValid.Should().BeFalse();
    }

    // --- Profile from Supabase claims ---

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void Profile_reads_full_name_then_name_and_avatar()
    {
        Principal(("user_metadata", """{"full_name":"Amira Ben Ali","name":"amira","avatar_url":"https://img/a.png"}"""))
            .GetProfileFromMetadata().Should().Be(("Amira Ben Ali", "https://img/a.png"));
        Principal(("user_metadata", """{"name":"amira"}"""))
            .GetProfileFromMetadata().Should().Be(("amira", (string?)null));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"full_name":42}""")]
    [InlineData("{}")]
    public void Invalid_metadata_falls_back_to_email_local_part(string metadata)
    {
        Principal(("user_metadata", metadata), (ClaimTypes.Email, "amira@example.com"))
            .GetProfileFromMetadata().Should().Be(("amira", (string?)null));
    }

    [Fact]
    public void No_metadata_and_no_email_gives_nulls()
    {
        Principal().GetProfileFromMetadata().Should().Be(((string?)null, (string?)null));
        Principal().GetEmail().Should().BeNull();
    }

    [Fact]
    public void Email_is_read_from_either_claim_name()
    {
        Principal(("email", "a@example.com")).GetEmail().Should().Be("a@example.com");
        Principal((ClaimTypes.Email, "b@example.com")).GetEmail().Should().Be("b@example.com");
    }
}
