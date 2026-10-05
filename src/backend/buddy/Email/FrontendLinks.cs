using Microsoft.Extensions.Options;

namespace buddy.Email;

// The clickable frontend links that carry a one-time token. One builder for the emails and for
// the API responses that hand the same link back to the sender to share themself, so the two can
// never drift apart.
public sealed class FrontendLinks(IOptionsMonitor<MailOptions> options)
{
    public string EmailVerification(string token) => Build("verify-email", token);

    public string GroupInvite(string token) => Build("invite", token);

    public string GuardianInvite(string token) => Build("guardian-invite", token);

    private string Build(string path, string token) =>
        $"{options.CurrentValue.FrontendBaseUrl.TrimEnd('/')}/{path}/{Uri.EscapeDataString(token)}";
}
