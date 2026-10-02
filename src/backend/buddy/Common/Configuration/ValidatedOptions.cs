using Microsoft.Extensions.Options;

namespace buddy.Common.Configuration;

// `required string` on an options class is only a compile-time promise: the configuration binder
// creates the instance reflectively and leaves any missing key null. Every options registration
// goes through here instead of services.Configure<T>(section), so a missing or blank value marked
// [Required] fails the host at startup (ValidateOnStart) rather than at first use, deep inside a
// request. BindConfiguration reads the DI IConfiguration, so overrides added after the features
// register (the integration-test fixture's ConfigurationOverride) are what gets validated.
public static class ValidatedOptions
{
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(this IServiceCollection services, string sectionName)
        where TOptions : class =>
        services.AddOptions<TOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
