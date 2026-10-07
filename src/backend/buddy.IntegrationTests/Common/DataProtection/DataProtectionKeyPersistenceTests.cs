using buddy.Common.DataProtection;
using buddy.IntegrationTests.Fixtures;

using Marten;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Common.DataProtection;

// The key ring must outlive the container: a redeploy that loses it makes every stored AI-provider
// API key and stored idempotent response undecryptable.
[Collection(BuddyApiCollection.Name)]
public sealed class DataProtectionKeyPersistenceTests(BuddyApiFixture fixture)
{
    private const string Purpose = "buddy.IntegrationTests.DataProtection";

    [Fact]
    public async Task Every_key_in_the_key_ring_is_stored_in_postgres()
    {
        // Protecting forces a key to exist.
        fixture.Host.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("secret");

        var keyIds = fixture.Host.Services.GetRequiredService<IKeyManager>().GetAllKeys().Select(k => $"key-{k.KeyId}").ToArray();

        await using var session = fixture.Host.Services.GetRequiredService<IDataProtectionStore>().QuerySession();
        var storedIds = (await session.Query<DataProtectionKeyDocument>().Select(d => d.Id).ToListAsync()).ToHashSet();

        Assert.NotEmpty(keyIds);
        Assert.All(keyIds, id => Assert.Contains(id, storedIds));
    }

    [Fact]
    public async Task A_new_host_can_decrypt_what_another_host_encrypted()
    {
        var cipherText = fixture.Host.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("secret");

        await using var newHost = await fixture.CreateHostAsync(new Dictionary<string, string?>());

        var plainText = newHost.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Unprotect(cipherText);

        Assert.Equal("secret", plainText);
    }
}
