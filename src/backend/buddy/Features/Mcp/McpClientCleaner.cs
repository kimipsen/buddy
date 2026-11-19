using System.Globalization;

using Microsoft.Extensions.Options;

namespace buddy.Features.Mcp;

// One pass of the MCP client cleanup (docs/backend/analysis/mcp-server.md, "Client registration"): deletes the
// dynamically registered clients nobody consented to within the grace period, so junk registrations can't fill
// Keycloak's max-clients policy and lock real clients out. Deletion can't be undone, so everything it can't
// prove keeps the client: a consent lookup that fails, a user list shorter than Keycloak's count, a first-seen
// stamp it can't read.
public sealed class McpClientCleaner(
    IKeycloakClientRegistry registry,
    TimeProvider time,
    IOptions<McpClientCleanupOptions> options,
    ILogger<McpClientCleaner> logger)
{
    // Long enough for a client that registers one day and is used the next; the setup guide's
    // troubleshooting names the recovery after a deletion (remove the connector, add it again).
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    // Keycloak's built-in clients and Buddy's own. None is a UUID, so the UUID rule already keeps them out;
    // the list and the "buddy-" prefix (which the setup guide asks operators to use for the clients they add
    // by hand) are a second, independent guard.
    private static readonly HashSet<string> ExcludedClientIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "account", "account-console", "admin-cli", "broker", "realm-management", "security-admin-console",
        "buddy-frontend", "buddy-admin-cli", "buddy-mcp"
    };

    private const string ExcludedPrefix = "buddy-";
    private const string McpScopePrefix = "buddy-mcp:";

    // In OIDC dynamic registration the server assigns the client_id and Keycloak makes it a UUID; a client
    // created by hand has a readable id. It must also carry a buddy-mcp scope, and be public, which an
    // anonymous registration always is (a policy refuses the rest): anything else someone made with intent.
    internal static bool IsRegisteredClient(KeycloakClientSummary client) =>
        Guid.TryParse(client.ClientId, out _)
        && !ExcludedClientIds.Contains(client.ClientId)
        && !client.ClientId.StartsWith(ExcludedPrefix, StringComparison.OrdinalIgnoreCase)
        && client.PublicClient
        && client.Scopes.Any(scope => scope.StartsWith(McpScopePrefix, StringComparison.Ordinal));

    public async Task<McpClientCleanupResult> RunAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var now = time.GetUtcNow();

        var candidates = (await registry.ListClientsAsync(cancellationToken)).Where(IsRegisteredClient).ToList();

        // The cheap check: with no registered client the users and consents are never read.
        var consented = candidates.Count == 0 ? [] : await TryReadConsentsAsync(cancellationToken);

        var counts = new Counts();
        var wouldDelete = new List<string>();
        var capHit = 0;
        foreach (var client in candidates)
        {
            if (consented is null)
            {
                counts.KeptUnknown++;
            }
            else if (consented.Contains(client.ClientId))
            {
                counts.Kept++;
            }
            else if (client.FirstSeen is null)
            {
                // The first pass that sees it only starts its clock; a dry run makes this one write too, so
                // the age rule can progress while the operator reads the summary.
                if (await TryAsync(counts, client, "stamp", () => registry.SetClientAttributeAsync(client.Id, KeycloakClientRegistry.FirstSeenAttribute, now.ToString("O", CultureInfo.InvariantCulture), cancellationToken)))
                {
                    counts.Stamped++;
                }
            }
            else if (!DateTimeOffset.TryParse(client.FirstSeen, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var firstSeen) || firstSeen > now)
            {
                // A stamp that doesn't parse or lies in the future (clock change, hand edit): not provably old.
                counts.KeptUnknown++;
            }
            else if (now - firstSeen < GracePeriod)
            {
                counts.Kept++;
            }
            else if (counts.Deleted + wouldDelete.Count >= settings.MaxDeletesPerPass)
            {
                capHit++;
                counts.Kept++;
            }
            else if (settings.DryRun)
            {
                wouldDelete.Add(client.ClientId);
            }
            else if (await TryAsync(counts, client, "delete", () => registry.DeleteClientAsync(client.Id, cancellationToken)))
            {
                counts.Deleted++;
            }
        }

        if (capHit > 0)
        {
            logger.McpClientCleanupCapReached(settings.MaxDeletesPerPass, capHit);
        }

        var result = new McpClientCleanupResult(
            settings.DryRun, candidates.Count, counts.Stamped, counts.Deleted, wouldDelete, counts.Kept, counts.KeptUnknown, counts.Failed, capHit);
        var wouldDeleteIds = string.Join(',', result.WouldDelete);
        logger.McpClientCleanupFinished(
            result.DryRun, result.Candidates, result.Stamped, result.Deleted, result.WouldDelete.Count, wouldDeleteIds, result.Kept, result.KeptUnknown, result.Failed);

        return result;
    }

    // Every consent of every user, or null when any of it can't be trusted: an exception or a non-2xx from
    // any call, an answer that doesn't parse, or fewer users than Keycloak counts (a short page that is not
    // the last is invisible from the pages alone, so the count is the second source). The caller keeps every
    // client for that pass and tries again at the next tick: a spurious keep is harmless, a spurious delete
    // is not.
    private async Task<HashSet<string>?> TryReadConsentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var count = await registry.CountUsersAsync(cancellationToken);
            var userIds = await registry.ListUserIdsAsync(cancellationToken);
            if (userIds.Count != count)
            {
                throw new InvalidOperationException($"Keycloak counts {count} users but the list had {userIds.Count}.");
            }

            var consented = new HashSet<string>(StringComparer.Ordinal);
            foreach (var userId in userIds)
            {
                consented.UnionWith(await registry.ListConsentedClientIdsAsync(userId, cancellationToken));
            }

            return consented;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.McpClientCleanupFailedClosed(ex);
            return null;
        }
    }

    // One client's failed write is logged and doesn't end the pass.
    private async Task<bool> TryAsync(Counts counts, KeycloakClientSummary client, string operation, Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.McpClientCleanupClientFailed(operation, client.ClientId, ex);
            counts.Failed++;
            return false;
        }
    }

    private sealed class Counts
    {
        public int Stamped;
        public int Deleted;
        public int Kept;
        public int KeptUnknown;
        public int Failed;
    }
}

public sealed record McpClientCleanupResult(
    bool DryRun,
    int Candidates,
    int Stamped,
    int Deleted,
    IReadOnlyList<string> WouldDelete,
    int Kept,
    int KeptUnknown,
    int Failed,
    int SkippedByCap);
