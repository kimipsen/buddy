using MailKit.Net.Smtp;
using MailKit.Security;

using Microsoft.Extensions.Options;

using MimeKit;

namespace buddy.Email;

public sealed class SmtpEmailSender(IOptionsMonitor<MailOptions> options, FrontendLinks links, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(string emailAddress, string token, CancellationToken cancellationToken)
    {
        var link = links.EmailVerification(token);
        return SendAsync("email-verification", emailAddress, "Verify your email address", $"Verify your email address by clicking the link below:\n\n{link}", cancellationToken);
    }

    public Task SendGroupInviteEmailAsync(string emailAddress, string groupName, string token, CancellationToken cancellationToken)
    {
        var link = links.GroupInvite(token);
        return SendAsync(
            "group-invite",
            emailAddress,
            $"You've been invited to join {groupName}",
            $"You've been invited to join the group \"{groupName}\". Click the link below to accept:\n\n{link}",
            cancellationToken);
    }

    public Task SendGuardianInviteEmailAsync(string emailAddress, string childGivenName, string token, CancellationToken cancellationToken)
    {
        var link = links.GuardianInvite(token);
        return SendAsync(
            "guardian-invite",
            emailAddress,
            $"You've been invited to help manage {childGivenName}'s account",
            $"You've been invited to help manage {childGivenName}'s account. Click the link below to accept:\n\n{link}",
            cancellationToken);
    }

    private async Task SendAsync(string emailKind, string emailAddress, string subject, string body, CancellationToken cancellationToken)
    {
        var mail = options.CurrentValue;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(mail.FromName ?? mail.FromAddress, mail.FromAddress));
        message.To.Add(MailboxAddress.Parse(emailAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(
                mail.Host,
                mail.Port,
                mail.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
                cancellationToken);

            // Only authenticate when credentials are configured and the server actually advertises
            // support for it -- lets Mailpit's unauthenticated SMTP keep working even if placeholder
            // credentials are set in the environment.
            if (mail.Credentials is { } credentials && client.Capabilities.HasFlag(SmtpCapabilities.Authentication))
            {
                await client.AuthenticateAsync(credentials.Username, credentials.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Still thrown: the caller's request fails as before. Logged here because only this
            // class knows which SMTP server it was talking to.
            logger.EmailSendFailed(exception, emailKind, mail.Host, mail.Port);
            throw;
        }

        logger.EmailSent(emailKind);
    }
}
