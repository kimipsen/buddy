using buddy.Common.Erasure;
using buddy.Common.Idempotency;
using buddy.IntegrationTests.Fixtures;

using Marten;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Meta;

// Every Marten store holds data about people, so every store needs an IPersonalDataExporter -- or a
// stated reason here why not. A new feature that forgets one fails this test instead of quietly
// leaving its data out of GET /users/me/export. See docs/backend/analysis/gdpr-data-protection.md.
[Collection(BuddyApiCollection.Name)]
public sealed class PersonalDataExporterCoverageTests(BuddyApiFixture fixture)
{
    private static readonly Dictionary<Type, string> Exempt = new()
    {
        // Encrypted copies of responses the caller already received, deleted after 24 hours.
        [typeof(IIdempotencyStore)] = "short-lived copies of responses",
    };

    [Fact]
    public void Every_marten_store_has_a_personal_data_exporter()
    {
        var stores = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsInterface && t != typeof(IDocumentStore) && typeof(IDocumentStore).IsAssignableFrom(t))
            .ToHashSet();

        var exporters = fixture.Host.Services.GetServices<IPersonalDataExporter>().ToArray();
        var covered = exporters.Select(e => e.Store).ToHashSet();

        var missing = stores.Except(covered).Except(Exempt.Keys).Select(t => t.Name).Order().ToArray();
        Assert.True(missing.Length == 0, $"These Marten stores have no IPersonalDataExporter: {string.Join(", ", missing)}");

        var stale = Exempt.Keys.Intersect(covered).Select(t => t.Name).ToArray();
        Assert.True(stale.Length == 0, $"These exempt stores have an exporter after all: {string.Join(", ", stale)}");

        var duplicates = exporters.GroupBy(e => e.Section).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.True(duplicates.Length == 0, $"These export sections are claimed by more than one exporter: {string.Join(", ", duplicates)}");
    }
}
