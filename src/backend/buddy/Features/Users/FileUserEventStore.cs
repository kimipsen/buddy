using System.Text.Json;
using buddy.Serialization;

namespace buddy.Features.Users;

public sealed class FileUserEventStore(IConfiguration configuration) : IUserEventStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new StronglyTypedIdJsonConverterFactory() }
    };
    private readonly string _filePath = configuration["EventStore:FilePath"] ?? "data/user-events.jsonl";

    public async Task<IReadOnlyCollection<UserEvent>> ReadAsync(string keycloakSubject, CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var userEvents = new List<UserEvent>();

            await foreach (var line in File.ReadLinesAsync(_filePath, cancellationToken))
            {
                var envelope = JsonSerializer.Deserialize<UserEventEnvelope>(line, _jsonOptions);

                if (envelope?.KeycloakSubject != keycloakSubject)
                {
                    continue;
                }

                if (envelope.Type == nameof(UserCreated))
                {
                    var created = envelope.Data.Deserialize<UserCreated>(_jsonOptions);

                    if (created is not null)
                    {
                        userEvents.Add(created);
                    }
                }
            }

            return userEvents;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AppendAsync(string keycloakSubject, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (events.Count == 0)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var stream = File.Open(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream);

            foreach (var @event in events)
            {
                var envelope = new UserEventEnvelope(
                    keycloakSubject,
                    @event.Value?.GetType().Name ?? throw new InvalidOperationException("Cannot persist an empty user event."),
                    JsonSerializer.SerializeToElement(@event.Value, @event.Value.GetType(), _jsonOptions));

                await writer.WriteLineAsync(JsonSerializer.Serialize(envelope, _jsonOptions));
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
