using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;

namespace buddy.Email;

public sealed class SmtpEmailSender(IOptionsMonitor<MailOptions> options) : IEmailSender
{
    public Task SendEmailVerificationAsync(string emailAddress, string token, CancellationToken cancellationToken) =>
        SendAsync(emailAddress, "Verify your email address", $"Your verification token is: {token}", cancellationToken);

    public Task SendGroupInviteEmailAsync(string emailAddress, string groupName, string token, CancellationToken cancellationToken) =>
        SendAsync(
            emailAddress,
            $"You've been invited to join {groupName}",
            $"You've been invited to join the group \"{groupName}\". Your invite token is: {token}",
            cancellationToken);

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
