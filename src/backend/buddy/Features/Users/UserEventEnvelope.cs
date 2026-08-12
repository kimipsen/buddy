using System.Text.Json;

namespace buddy.Features.Users;

public sealed record UserEventEnvelope(string KeycloakSubject, string Type, JsonElement Data);
