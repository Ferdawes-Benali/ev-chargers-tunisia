namespace EvChargers.Application.Email;

/// <summary>One email to send. Html is the full body.</summary>
public record EmailMessage(string To, string Subject, string Html);