using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;

namespace buddy.Email;

public sealed class SmtpEmailSender(IOptionsMonitor<MailOptions> options) : IEmailSender
{
    public Task SendEmailVerificationAsync(string emailAddress, string token, CancellationToken cancellationToken)
    {
        var link = BuildLink("verify-email", token);
        return SendAsync(emailAddress, "Verify your email address", $"Verify your email address by clicking the link below:\n\n{link}", cancellationToken);
    }

    public Task SendGroupInviteEmailAsync(string emailAddress, string groupName, string token, CancellationToken cancellationToken)
    {
        var link = BuildLink("invite", token);
        return SendAsync(
            emailAddress,
            $"You've been invited to join {groupName}",
            $"You've been invited to join the group \"{groupName}\". Click the link below to accept:\n\n{link}",
            cancellationToken);
    }

    public Task SendGuardianInviteEmailAsync(string emailAddress, string childGivenName, string token, CancellationToken cancellationToken)
    {
        var link = BuildLink("guardian-invite", token);
        return SendAsync(
            emailAddress,
            $"You've been invited to help manage {childGivenName}'s account",
            $"You've been invited to help manage {childGivenName}'s account. Click the link below to accept:\n\n{link}",
            cancellationToken);
    }

    private string BuildLink(string path, string token) =>
        $"{options.CurrentValue.FrontendBaseUrl.TrimEnd('/')}/{path}/{Uri.EscapeDataString(token)}";

    private async Task SendAsync(string emailAddress, string subject, string body, CancellationToken cancellationToken)
    {
        var mail = options.CurrentValue;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(mail.FromName ?? mail.FromAddress, mail.FromAddress));
        message.To.Add(MailboxAddress.Parse(emailAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            mail.Host,
            mail.Port,
            mail.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
            cancellationToken);

        // Only authenticate when both a username is configured and the server actually
        // advertises support for it -- lets Mailpit's unauthenticated SMTP keep working
        // even if placeholder credentials are set in the environment.
        if (!string.IsNullOrEmpty(mail.Username) && client.Capabilities.HasFlag(SmtpCapabilities.Authentication))
        {
            await client.AuthenticateAsync(mail.Username, mail.Password ?? "", cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
