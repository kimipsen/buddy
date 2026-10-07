using buddy.Common.Versioning;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Xunit;

namespace buddy.IntegrationTests.Common.Versioning;

[Collection(BuddyApiCollection.Name)]
public sealed class VersionTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetVersion")]
    public async Task An_anonymous_caller_gets_the_running_build_version()
    {
        var response = await fixture.Host.Scenario(_ =>
        {
            _.Get.Url("/version");
            _.StatusCodeShouldBe(200);
        });

        var version = response.ReadAsJson<BuildVersion>();
        Assert.Equal(BuildVersion.Current, version);
        Assert.Matches(@"^\d+\.\d+\.\d+", version.Version);
    }

    [Theory]
    [InlineData("1.2.3+abc123", "1.2.3", "abc123")]
    [InlineData("1.3.0-alpha.0.4+abc123", "1.3.0-alpha.0.4", "abc123")]
    [InlineData("1.2.3", "1.2.3", null)]
    [InlineData(null, "0.0.0", null)]
    [InlineData(" ", "0.0.0", null)]
    public void The_informational_version_splits_into_version_and_commit(string? informationalVersion, string expectedVersion, string? expectedCommit)
    {
        Assert.Equal(new BuildVersion(expectedVersion, expectedCommit), BuildVersion.FromInformationalVersion(informationalVersion));
    }
}
