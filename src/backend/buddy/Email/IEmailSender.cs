namespace buddy.Email;

public interface IEmailSender
{
    Task SendEmailVerificationAsync(string emailAddress, string token, CancellationToken cancellationToken);

    Task SendGroupInviteEmailAsync(string emailAddress, string groupName, string token, CancellationToken cancellationToken);
}
