using System.Reflection;

namespace buddy.Common.Versioning;

// The running build's version. MinVer sets the assembly's informational version from the nearest
// vX.Y.Z git tag, and the SDK appends "+<commit sha>" from the git checkout (or from the
// SourceRevisionId build property, which the Dockerfile passes in since its context has no .git).
public sealed record BuildVersion(string Version, string? Commit)
{
    public static BuildVersion Current { get; } = FromInformationalVersion(
        typeof(BuildVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    internal static BuildVersion FromInformationalVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return new BuildVersion("0.0.0", null);
        }

        var plus = informationalVersion.IndexOf('+');
        return plus < 0
            ? new BuildVersion(informationalVersion, null)
            : new BuildVersion(informationalVersion[..plus], informationalVersion[(plus + 1)..]);
    }
}
