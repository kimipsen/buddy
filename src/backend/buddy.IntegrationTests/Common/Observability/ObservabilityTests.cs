using System.Collections.Concurrent;
using System.Diagnostics;

using Alba;

using buddy.Common;
using buddy.IntegrationTests.Fixtures;

using OpenTelemetry;
using OpenTelemetry.Trace;

using Xunit;

namespace buddy.IntegrationTests.Common.Observability;

[Collection(BuddyApiCollection.Name)]
public sealed class ObservabilityTests(BuddyApiFixture fixture) : IAsyncLifetime
{
    private readonly ConcurrentQueue<Activity> _exported = new();

    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await fixture.CreateHostAsync(
            new Dictionary<string, string?>(),
            services => services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(new CollectingProcessor(_exported))));
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task A_request_is_traced_down_to_its_database_queries()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await _host.Scenario(_ =>
        {
            _.Get.Url("/users/me");
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.StatusCodeShouldBe(200);
        });

        var server = await WaitForAsync(a => a.Kind == ActivityKind.Server && Equals(a.GetTagItem("http.route"), "/users/me"));

        var queries = Exported().Where(a => a.Source.Name == "Npgsql" && a.TraceId == server.TraceId).ToArray();
        Assert.NotEmpty(queries);
    }

    [Fact]
    public async Task Health_probes_are_not_traced()
    {
        await _host.Scenario(_ =>
        {
            _.Get.Url("/health/ready");
            _.StatusCodeShouldBe(200);
        });

        // A traced request after the probe, so the probe's span would have been exported by now.
        await _host.Scenario(_ =>
        {
            _.Get.Url("/version");
            _.StatusCodeShouldBe(200);
        });
        await WaitForAsync(a => a.Kind == ActivityKind.Server && Equals(a.GetTagItem("http.route"), "/version"));

        Assert.DoesNotContain(Exported(), a => a.Kind == ActivityKind.Server
            && (a.GetTagItem("url.path") as string ?? "").StartsWith("/health", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_secret_token_in_the_path_never_reaches_a_span()
    {
        var token = $"secret{Guid.NewGuid():N}";

        await _host.Scenario(_ =>
        {
            _.Get.Url($"/sleep-diary/shared/{token}");
            _.IgnoreStatusCode();
        });

        var server = await WaitForAsync(a => a.Kind == ActivityKind.Server
            && (a.GetTagItem("http.route") as string ?? "").Contains("{token}", StringComparison.Ordinal));

        Assert.Equal("/sleep-diary/shared/{token}", server.GetTagItem("url.path"));
        Assert.DoesNotContain(server.TagObjects, tag => tag.Value is string value && value.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_error_responses_request_id_is_its_trace_id()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await _host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            // No Name: rejected by request binding with an ErrorEnvelope.
            _.Post.Json(new { }).ToUrl("/groups/");
            _.StatusCodeShouldBe(400);
        });
        var requestId = response.ReadAsJson<ErrorEnvelope>().RequestId;

        var server = await WaitForAsync(a => a.Kind == ActivityKind.Server && a.TraceId.ToHexString() == requestId);
        Assert.Equal("POST", server.GetTagItem("http.request.method"));
    }

    private Activity[] Exported() => [.. _exported];

    // A server span is exported when it stops, which can be just after the response reaches the test.
    private async Task<Activity> WaitForAsync(Func<Activity, bool> predicate)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (Exported().FirstOrDefault(predicate) is { } activity)
            {
                return activity;
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException("Expected span was never exported.");
    }

    // Every span the SDK records (sampled and not filtered out), as it ends.
    private sealed class CollectingProcessor(ConcurrentQueue<Activity> spans) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data) => spans.Enqueue(data);
    }
}
