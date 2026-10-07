using System.Threading.RateLimiting;

namespace buddy.Common.RateLimiting;

// Every number behind RateLimitingFeature, bound from "RateLimiting:*". The defaults are the ones
// argued in docs/backend/analysis/rate-limiting.md; production re-tunes through configuration.
// Classes with setters rather than records: the configuration binder fills the nested defaults in
// place, so a deployment can override one value (RateLimiting__IcalFeed__TokenLimit) and keep the rest.
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // Per Keycloak subject. A dashboard load is a burst (one request per child per widget), then quiet.
    public TokenBucketLimits Authenticated { get; set; } = new() { TokenLimit = 200, TokensPerPeriod = 5, ReplenishmentPeriod = TimeSpan.FromSeconds(1) };

    // Per client IP, for every request without a valid token.
    public TokenBucketLimits Anonymous { get; set; } = new() { TokenLimit = 60, TokensPerPeriod = 1, ReplenishmentPeriod = TimeSpan.FromSeconds(1) };

    // Per feed link (feed id + token hash). The feeds advertise an hourly refresh.
    public TokenBucketLimits IcalFeed { get; set; } = new() { TokenLimit = 10, TokensPerPeriod = 1, ReplenishmentPeriod = TimeSpan.FromMinutes(5) };

    // Per user, on the endpoints that call the family's LLM provider.
    public FixedWindowLimits AiAssistant { get; set; } = new() { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) };

    // Per user, on the endpoints that send email.
    public FixedWindowLimits OutboundEmail { get; set; } = new() { PermitLimit = 20, Window = TimeSpan.FromHours(1) };

    // Per user, on GET /users/me/export (docs/backend/analysis/gdpr-data-protection.md, Question 5).
    public FixedWindowLimits PersonalDataExport { get; set; } = new() { PermitLimit = 1, Window = TimeSpan.FromMinutes(10) };

    public bool IsValid() =>
        Authenticated.IsValid() && Anonymous.IsValid() && IcalFeed.IsValid() && AiAssistant.IsValid() && OutboundEmail.IsValid()
        && PersonalDataExport.IsValid();
}

public sealed class TokenBucketLimits
{
    public int TokenLimit { get; set; }

    public int TokensPerPeriod { get; set; }

    public TimeSpan ReplenishmentPeriod { get; set; }

    public bool IsValid() => TokenLimit > 0 && TokensPerPeriod > 0 && ReplenishmentPeriod > TimeSpan.Zero;

    // QueueLimit 0: a request over the limit is rejected at once, never held open waiting for a token.
    public TokenBucketRateLimiterOptions ToOptions() => new()
    {
        TokenLimit = TokenLimit,
        TokensPerPeriod = TokensPerPeriod,
        ReplenishmentPeriod = ReplenishmentPeriod,
        QueueLimit = 0,
        AutoReplenishment = true
    };
}

public sealed class FixedWindowLimits
{
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }

    public bool IsValid() => PermitLimit > 0 && Window > TimeSpan.Zero;

    public FixedWindowRateLimiterOptions ToOptions() => new()
    {
        PermitLimit = PermitLimit,
        Window = Window,
        QueueLimit = 0,
        AutoReplenishment = true
    };
}
