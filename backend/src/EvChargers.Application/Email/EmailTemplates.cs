using System.Net;
using EvChargers.Application.Common;

namespace EvChargers.Application.Email;

public static class EmailTemplates
{
    private const string Brand = "#0e9d9f";
    private const string Ink = "#1E293B";
    private const string Muted = "#64748B";

    public static EmailMessage Welcome(string to, string? displayName, string? language)
    {
        var lang = Languages.Normalize(language);
        var name = Encode(displayName) ?? "";
        var (subject, title, body) = lang switch
        {
            "ar" => ("مرحبًا بك في EV Chargers Tunisia", "مرحبًا بك", $"""
                <p style="margin:0 0 12px">{Greeting(lang, name)}</p>
                <p style="margin:0 0 12px">أهلاً بك في <strong>EV Chargers Tunisia</strong>&rlm;! يمكنك الآن العثور على محطات الشحن
                وتخطيط رحلاتك، ومشاركة آرائك مع المجتمع.</p>
                """),
            "en" => ("Welcome to EV Chargers Tunisia", "Welcome", $"""
                <p style="margin:0 0 12px">{Greeting(lang, name)}</p>
                <p style="margin:0 0 12px">Welcome to <strong>EV Chargers Tunisia</strong>! You can now
                find charging stations, plan your trips and share your reviews with the community.</p>
                """),
            _ => ("Bienvenue sur EV Chargers Tunisia", "Bienvenue", $"""
                <p style="margin:0 0 12px">{Greeting(lang, name)}</p>
                <p style="margin:0 0 12px">Bienvenue sur <strong>EV Chargers Tunisia</strong> ! Vous pouvez maintenant
                trouver des bornes de recharge, planifier vos trajets et partager vos avis avec la communauté.</p>
                """),
        };
        return new EmailMessage(to, subject, Layout(lang, title, body));
    }

    public static EmailMessage ReviewConfirmation(string to, string? displayName, string stationName, int rating, string? language)
    {
        var lang = Languages.Normalize(language);
        var name = Encode(displayName) ?? "";
        var station = Encode(stationName);
        var filled = Math.Clamp(rating, 0, 5);
        var stars = $"""<p style="margin:0 0 12px;font-size:22px;color:{Brand};letter-spacing:2px">{new string('★', filled)}{new string('☆', 5 - filled)}</p>""";
        var (subject, title, body) = lang switch
        {
            "ar" => ($"شكرًا على تقييمك لمحطة {stationName}", "تم تسجيل تقييمك ✓", $"""
                <p style="margin:0 0 12px">{Greeting(lang, name)}</p>
                <p style="margin:0 0 12px">شكرًا على تقييمك لمحطة <strong>{station}</strong>.</p>
                {stars}
                <p style="margin:0 0 12px">ملاحظاتك تساعد السائقين الآخرين على إيجاد محطات موثوقة.</p>
                """),
            "en" => ($"Thanks for your review of {stationName}", "Review saved ✓", $"""
                <p style="margin:0 0 12px">{Greeting(lang, name)}</p>
                <p style="margin:0 0 12px">Thank you for your review of <strong>{station}</strong>.</p>
                {stars}
                <p style="margin:0 0 12px">Your feedback helps other drivers find reliable charging stations.</p>
                """),
            _ => ($"Merci pour votre avis sur {stationName}", "Avis enregistré ✓", $"""
                <p style="margin:0 0 12px">{Greeting(lang, name)}</p>
                <p style="margin:0 0 12px">Merci pour votre avis sur <strong>{station}</strong>.</p>
                {stars}
                <p style="margin:0 0 12px">Vos retours aident les autres conducteurs à trouver des bornes fiables.</p>
                """),
        };
        return new EmailMessage(to, subject, Layout(lang, title, body));
    }

    private static string Greeting(string lang, string encodedName) => (lang, encodedName.Length > 0) switch
    {
        ("ar", true) => $"مرحبًا {encodedName}،",
        ("ar", false) => "مرحبًا،",
        ("en", true) => $"Hello {encodedName},",
        ("en", false) => "Hello,",
        (_, true) => $"Bonjour {encodedName},",
        _ => "Bonjour,",
    };

    private static string Footer(string lang) => lang switch
    {
        "ar" => "تتلقى هذه الرسالة لأن لديك حسابًا على EV Chargers Tunisia&rlm;. هذه رسالة تلقائية، يُرجى عدم الرد عليها.",
        "en" => "You are receiving this email because you have an EV Chargers Tunisia account. This email is sent automatically, please do not reply.",
        _ => "Vous recevez cet e-mail car vous avez un compte EV Chargers Tunisia. Cet e-mail est envoyé automatiquement, merci de ne pas y répondre.",
    };

    private static string Layout(string lang, string title, string contentHtml)
    {
        var rtl = lang == "ar";
        // dir/lang go on <html> and again on each text cell: some clients drop the <html> attributes
        var dirAttrs = rtl ? " dir=\"rtl\" lang=\"ar\"" : "";
        var htmlDir = rtl ? " dir=\"rtl\"" : "";
        var font = rtl ? "Tahoma,Arial,sans-serif" : "Arial,Helvetica,sans-serif";
        var align = rtl ? "text-align:right;" : "";

        return $"""
            <!DOCTYPE html>
            <html lang="{lang}"{htmlDir}>
            <body style="margin:0;padding:0;background:#F1F5F9">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#F1F5F9;padding:24px 12px">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0"
                         style="max-width:600px;background:#FFFFFF;border-radius:12px;overflow:hidden;font-family:{font};color:{Ink};font-size:15px;line-height:1.6">
                    <tr><td style="background:{Brand};padding:20px 28px;color:#FFFFFF;font-size:20px;font-weight:bold">
                        EV Chargers Tunisia
                    </td></tr>
                    <tr><td{dirAttrs} style="padding:28px 28px 16px;{align}font-family:{font}">
                      <h1 style="margin:0 0 16px;font-size:22px;color:{Ink}">{title}</h1>
                      {contentHtml}
                    </td></tr>
                    <tr><td{dirAttrs} style="background:#F8FAFC;padding:16px 28px;color:{Muted};font-size:12px;line-height:1.5;{align}font-family:{font}">
                        {Footer(lang)}
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string? Encode(string? text) => text is null ? null : WebUtility.HtmlEncode(text);
}
