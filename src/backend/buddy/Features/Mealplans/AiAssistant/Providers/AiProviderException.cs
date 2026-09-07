using System.Net;

namespace buddy.Features.Mealplans;

// Surfaces a failed provider HTTP call (bad/expired key, no quota, provider outage, ...) up to the
// handler as a typed, catchable failure rather than an unhandled exception -- SendAiSessionMessage
// and TestProviderConnection both need to turn this into a Result<T> instead of a 500.
public sealed class AiProviderException(HttpStatusCode statusCode, string responseBody)
    : Exception($"AI provider request failed with status {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string ResponseBody { get; } = responseBody;
}
