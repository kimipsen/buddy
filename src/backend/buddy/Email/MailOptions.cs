namespace buddy.Email;

public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public required string Host { get; init; }

    public int Port { get; init; } = 25;

    // Toggles implicit TLS (SMTPS, typically port 465) vs. opportunistic STARTTLS on connect.
    // Mailpit needs neither; a stricter provider later can require either by flipping this.
    public bool UseSsl { get; init; }

    // Left null for Mailpit, which accepts unauthenticated SMTP. Set both to switch a
    // future provider over to authenticated sending -- SmtpEmailSender only authenticates
    // when a username is configured and the server advertises support for it.
    public string? Username { get; init; }

    public string? Password { get; init; }

    public required string FromAddress { get; init; }

    public string? FromName { get; init; }
}
