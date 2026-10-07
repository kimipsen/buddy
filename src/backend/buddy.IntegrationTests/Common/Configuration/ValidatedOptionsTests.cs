using buddy.Common.Postgres;
using buddy.Email;
using buddy.Features.Guardians;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit;

namespace buddy.IntegrationTests.Common.Configuration;

// The options registrations go through AddValidatedOptions (ValidateOnStart), so the host's
// startup validation -- the IAsyncStartupValidator the generic host runs before serving -- rejects a
// missing required value. Exercised on a bare ServiceCollection rather than a whole host.
public sealed class ValidatedOptionsTests
{
    private static readonly Dictionary<string, string?> ValidMail = new()
    {
        ["Mail:Host"] = "mailpit",
        ["Mail:FromAddress"] = "no-reply@buddy.local",
        ["Mail:FrontendBaseUrl"] = "http://localhost:4300",
    };

    [Fact]
    public async Task Valid_mail_settings_pass_startup_validation_without_credentials()
    {
        using var provider = BuildMail(ValidMail);

        await provider.GetRequiredService<IAsyncStartupValidator>().ValidateAsync();

        Assert.Null(provider.GetRequiredService<IOptions<MailOptions>>().Value.Credentials);
    }

    [Theory]
    [InlineData("Mail:Host")]
    [InlineData("Mail:FromAddress")]
    [InlineData("Mail:FrontendBaseUrl")]
    public async Task A_missing_required_mail_setting_fails_startup(string key)
    {
        using var provider = BuildMail(ValidMail.Where(entry => entry.Key != key));

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => provider.GetRequiredService<IAsyncStartupValidator>().ValidateAsync());
        Assert.Contains(key.Split(':')[1], error.Message);
    }

    [Fact]
    public async Task Smtp_credentials_bind_as_one_object()
    {
        using var provider = BuildMail(ValidMail.Concat(new Dictionary<string, string?>
        {
            ["Mail:Credentials:Username"] = "mailer",
            ["Mail:Credentials:Password"] = "secret",
        }));

        await provider.GetRequiredService<IAsyncStartupValidator>().ValidateAsync();

        var credentials = provider.GetRequiredService<IOptions<MailOptions>>().Value.Credentials;
        Assert.NotNull(credentials);
        Assert.Equal("mailer", credentials.Username);
        Assert.Equal("secret", credentials.Password);
    }

    [Fact]
    public async Task Blank_smtp_credentials_mean_unauthenticated_sending()
    {
        using var provider = BuildMail(ValidMail.Concat(new Dictionary<string, string?>
        {
            ["Mail:Credentials:Username"] = "",
            ["Mail:Credentials:Password"] = "",
        }));

        await provider.GetRequiredService<IAsyncStartupValidator>().ValidateAsync();

        Assert.Null(provider.GetRequiredService<IOptions<MailOptions>>().Value.Credentials);
    }

    [Fact]
    public async Task A_username_without_a_password_fails_startup()
    {
        using var provider = BuildMail(ValidMail.Append(new("Mail:Credentials:Username", "mailer")));

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => provider.GetRequiredService<IAsyncStartupValidator>().ValidateAsync());
        Assert.Contains("Credentials", error.Message);
    }

    [Fact]
    public async Task A_missing_connection_string_fails_startup()
    {
        var services = Services([]);
        services.AddPostgresDataSource(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        await Assert.ThrowsAsync<OptionsValidationException>(() => provider.GetRequiredService<IAsyncStartupValidator>().ValidateAsync());
    }

    [Fact]
    public async Task A_missing_keycloak_admin_client_secret_fails_startup_but_an_empty_one_is_allowed()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Authentication:KeycloakAdmin:Realm"] = "buddy",
            ["Authentication:KeycloakAdmin:TokenEndpoint"] = "http://keycloak/token",
            ["Authentication:KeycloakAdmin:AdminBaseUrl"] = "http://keycloak/admin/realms/buddy",
            ["Authentication:KeycloakAdmin:ClientId"] = "buddy-admin-cli",
        };

        using (var missing = BuildKeycloakAdmin(settings))
        {
            await Assert.ThrowsAsync<OptionsValidationException>(() => missing.GetRequiredService<IAsyncStartupValidator>().ValidateAsync());
        }

        settings["Authentication:KeycloakAdmin:ClientSecret"] = "";
        using var empty = BuildKeycloakAdmin(settings);
        await empty.GetRequiredService<IAsyncStartupValidator>().ValidateAsync();
    }

    private static ServiceProvider BuildMail(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var services = Services(settings);
        services.AddEmail(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildKeycloakAdmin(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var services = Services(settings);
        services.AddGuardiansFeature(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    // BindConfiguration reads the IConfiguration registered in DI, as in the real host.
    private static ServiceCollection Services(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return services;
    }
}
