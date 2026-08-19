using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;

namespace buddy.Email;

public sealed class SmtpEmailSender(IOptionsMonitor<MailOptions> options) : IEmailSender
{
    public async Task SendEmailVerificationAsync(string emailAddress, string token, CancellationToken cancellationToken)
    {
        var mail = options.CurrentValue;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(mail.FromName ?? mail.FromAddress, mail.FromAddress));
        message.To.Add(MailboxAddress.Parse(emailAddress));
        message.Subject = "Verify your email address";
        message.Body = new TextPart("plain") { Text = $"Your verification token is: {token}" };

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
