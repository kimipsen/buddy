namespace buddy.Email;

public interface IEmailSender
{
    Task SendEmailVerificationAsync(string emailAddress, string token, CancellationToken cancellationToken);
}
