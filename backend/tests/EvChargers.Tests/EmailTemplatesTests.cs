using FluentAssertions;
using EvChargers.Application.Email;
using Xunit;

namespace EvChargers.Tests;

public class EmailTemplatesTests
{
    [Fact]
    public void Arabic_email_is_right_to_left_with_tahoma()
    {
        var html = EmailTemplates.Welcome("a@example.com", "Amira", "ar").Html;

        html.Should().Contain("<html lang=\"ar\" dir=\"rtl\">");
        html.Should().Contain("<td dir=\"rtl\" lang=\"ar\"");
        html.Should().Contain("text-align:right");
        html.Should().Contain("Tahoma");
        html.Should().Contain("EV Chargers Tunisia</strong>&rlm;");
        html.Should().NotContain("Bienvenue");
    }

    [Fact]
    public void Each_email_has_a_single_language()
    {
        var fr = EmailTemplates.ReviewConfirmation("a@example.com", "Amira", "Borne X", 4, "fr");
        var en = EmailTemplates.ReviewConfirmation("a@example.com", "Amira", "Borne X", 4, "en");

        fr.Html.Should().Contain("Merci pour votre avis").And.NotContain("شكرًا").And.NotContain("Thank you");
        en.Html.Should().Contain("Thank you for your review").And.NotContain("شكرًا").And.NotContain("Merci");
        en.Html.Should().NotContain("dir=\"rtl\"");
        fr.Html.Should().Contain("★★★★☆");
    }

    [Theory]
    [InlineData("de")]
    [InlineData(null)]
    [InlineData("")]
    public void Unknown_language_falls_back_to_french(string? language)
    {
        EmailTemplates.Welcome("a@example.com", "Amira", language).Subject.Should().Be("Bienvenue sur EV Chargers Tunisia");
    }

    [Fact]
    public void User_text_is_html_encoded()
    {
        var html = EmailTemplates.ReviewConfirmation("a@example.com", "<b>x</b>", "<script>s</script>", 5, "en").Html;

        html.Should().NotContain("<script>").And.NotContain("<b>x</b>");
        html.Should().Contain("&lt;script&gt;");
    }
}
