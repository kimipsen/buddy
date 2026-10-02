using System.ComponentModel.DataAnnotations;

using buddy.Common.Configuration;

namespace buddy.Email;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        // ValidateDataAnnotations doesn't descend into nested objects, so the optional
        // Credentials block is validated explicitly: present means both values are set.
        services.AddValidatedOptions<MailOptions>(MailOptions.SectionName)
            .PostConfigure(mail =>
            {
                if (mail.Credentials is { } credentials
                    && string.IsNullOrEmpty(credentials.Username)
                    && string.IsNullOrEmpty(credentials.Password))
                {
                    mail.Credentials = null;
                }
            })
            .Validate(
                mail => mail.Credentials is null
                    || Validator.TryValidateObject(mail.Credentials, new ValidationContext(mail.Credentials), null, validateAllProperties: true),
                $"{MailOptions.SectionName}:Credentials needs both a Username and a Password.");
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
