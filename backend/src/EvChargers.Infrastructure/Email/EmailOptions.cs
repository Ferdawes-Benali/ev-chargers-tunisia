namespace EvChargers.Infrastructure.Email;

/// <summary>Sender settings, bound from the "Email" section (env vars: Email__FromAddress, Email__FromName).</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Resend's shared test sender; only used as the Development default.</summary>
    public const string DevelopmentFromAddress = "onboarding@resend.dev";

    /// <summary>Bare address, e.g. "noreply@example.tn". Required outside Development.</summary>
    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "EV Chargers Tunisia";

    /// <summary>
    /// Development only: every email goes to this inbox instead (Resend test mode only delivers to the account owner).
    /// Ignored, with a startup warning, in any other environment.
    /// </summary>
    public string? DevRedirectTo { get; set; }
}

/// <summary>Resend API settings, bound from the "Resend" section (env var: Resend__ApiKey).</summary>
public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    /// <summary>Required outside Development; in Development a missing key only skips sending.</summary>
    public string? ApiKey { get; set; }
}
