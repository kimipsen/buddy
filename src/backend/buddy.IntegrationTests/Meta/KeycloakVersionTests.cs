using System.Text.RegularExpressions;

using buddy.IntegrationTests.Fixtures;

using Xunit;

namespace buddy.IntegrationTests.Meta;

// The integration tests must run the Keycloak the app runs on. They once stayed on 21.1.1 while
// the devcontainer and the Azure image moved to 26.x, so 26.x-only behaviour (the "basic" scope
// carrying `sub`, the declarative user profile's VERIFY_PROFILE) passed here and broke e2e on a
// fresh realm. No containers needed: the image references are read from the checked-in files.
public sealed partial class KeycloakVersionTests
{
    [Theory]
    [InlineData(".devcontainer/docker-compose.yml")]
    [InlineData("deploy/azure/keycloak/Dockerfile")]
    public void The_fixture_runs_the_same_Keycloak_image_as(string path)
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), path));

        var images = KeycloakImageReference().Matches(text).Select(match => match.Groups["image"].Value).Distinct().ToArray();

        Assert.True(images.Length > 0, $"No quay.io/keycloak/keycloak image reference in {path}.");
        Assert.All(images, image => Assert.Equal(BuddyApiFixture.KeycloakImage, image));
    }

    // The tag, without a trailing "@sha256:..." digest pin.
    [GeneratedRegex(@"(?<image>quay\.io/keycloak/keycloak:[^\s@""']+)")]
    private static partial Regex KeycloakImageReference();

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "docs", "backend")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No repository root (a folder with docs/backend) above {AppContext.BaseDirectory}.");
    }
}
