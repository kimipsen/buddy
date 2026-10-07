using buddy.Common.DataProtection;
using buddy.Common.Erasure;
using buddy.Common.Idempotency;
using buddy.IntegrationTests.Fixtures;

using Marten;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Meta;

// Every Marten store holds data about people, so every store needs an IPersonalDataEraser -- or a
// stated reason here why not. A new feature that forgets one fails this test instead of quietly
// keeping data after an erasure. See docs/backend/analysis/gdpr-data-protection.md.
[Collection(BuddyApiCollection.Name)]
public sealed class PersonalDataEraserCoverageTests(BuddyApiFixture fixture)
{
    private static readonly Dictionary<Type, string> Exempt = new()
    {
        // Encrypted response bodies, deleted after 24 hours by IdempotencyCleanupService.
        [typeof(IIdempotencyStore)] = "short-lived and encrypted",
        // Encryption keys only; nothing about a person.
        [typeof(IDataProtectionStore)] = "encryption keys, no personal data",
    };

    [Fact]
    public void Every_marten_store_has_a_personal_data_eraser()
    {
        var stores = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsInterface && t != typeof(IDocumentStore) && typeof(IDocumentStore).IsAssignableFrom(t))
            .ToHashSet();

        var covered = fixture.Host.Services.GetServices<IPersonalDataEraser>().Select(e => e.Store).ToHashSet();

        var missing = stores.Except(covered).Except(Exempt.Keys).Select(t => t.Name).Order().ToArray();
        Assert.True(missing.Length == 0, $"These Marten stores have no IPersonalDataEraser: {string.Join(", ", missing)}");

        var stale = Exempt.Keys.Intersect(covered).Select(t => t.Name).ToArray();
        Assert.True(stale.Length == 0, $"These exempt stores have an eraser after all: {string.Join(", ", stale)}");
    }
}
