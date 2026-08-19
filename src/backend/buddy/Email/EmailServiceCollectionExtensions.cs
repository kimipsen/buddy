namespace buddy.Email;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
