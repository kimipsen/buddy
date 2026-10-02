using System.ComponentModel.DataAnnotations;

namespace buddy.Email;

public sealed class MailOptions
{
    public const string SectionName = "Mail";

    [Required]
    public required string Host { get; init; }

    public int Port { get; init; } = 25;

    // Toggles implicit TLS (SMTPS, typically port 465) vs. opportunistic STARTTLS on connect.
    // Mailpit needs neither; a stricter provider later can require either by flipping this.
    public bool UseSsl { get; init; }

    // Null (no Mail:Credentials section) for Mailpit, which accepts unauthenticated SMTP. Set it to
    // switch a provider over to authenticated sending -- SmtpEmailSender only authenticates when
    // credentials are configured and the server advertises support for it. One object, so a
    // username can't be configured without its password (validated at startup). Both values
    // blank -- what docker compose substitutes for unset MAILPIT_USERNAME/PASSWORD -- counts as no
    // credentials (AddEmail post-configures it back to null).
    public SmtpCredentials? Credentials { get; set; }

    [Required]
    public required string FromAddress { get; init; }

    public string? FromName { get; init; }

    // Base origin of the deployed frontend, used to build the clickable links sent in
    // verification and invite emails (e.g. "{FrontendBaseUrl}/invite/{token}"). No trailing slash.
    [Required]
    public required string FrontendBaseUrl { get; init; }
}

// Mail:Credentials:Username / Mail:Credentials:Password (env: Mail__Credentials__Username, ...).
public sealed class SmtpCredentials
{
    [Required]
    public required string Username { get; init; }

    [Required]
    public required string Password { get; init; }
}
