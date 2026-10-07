using buddy.Common.Postgres;

using Marten;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

using Npgsql;

namespace buddy.Common.DataProtection;

// The Data Protection key ring lives in Postgres, not in the container's home directory, so it
// survives a redeploy and is shared by every replica. Losing it makes everything encrypted with it
// unreadable: stored AI-provider API keys (DataProtectionApiKeyCipher) and stored idempotent
// responses (IdempotencyKeyRepository).
//
// The keys are stored unencrypted, as they were on disk before; anyone who can read the database
// can decrypt what they protect. Encrypting them at rest (ProtectKeysWithCertificate) is a follow-up.
public static class DataProtectionFeature
{
    public const string ApplicationName = "buddy";

    // Idempotent: every feature that encrypts something calls it, so each one stays self-contained.
    public static IServiceCollection AddDataProtectionFeature(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IDataProtectionStore)))
        {
            return services;
        }

        // Shared process-wide pool -- see PostgresDataSource.
        services.AddPostgresDataSource(configuration);

        services.AddMartenStore<IDataProtectionStore>(serviceProvider =>
        {
            var options = new StoreOptions();
            options.Connection(serviceProvider.GetRequiredService<NpgsqlDataSource>());
            options.DatabaseSchemaName = "dataprotection";
            options.UseSystemTextJsonForSerialization();

            return options;
        });

        // A fixed application name, so the keys don't depend on the content root path of the host.
        services.AddDataProtection().SetApplicationName(ApplicationName);
        services.AddOptions<KeyManagementOptions>()
            .Configure<IDataProtectionStore>((options, store) => options.XmlRepository = new MartenXmlRepository(store));

        return services;
    }
}
