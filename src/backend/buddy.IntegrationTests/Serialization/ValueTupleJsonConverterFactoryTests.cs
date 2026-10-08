using System.Text.Json;

using buddy.Serialization;

using Xunit;

namespace buddy.IntegrationTests.Serialization;

// Pure serializer tests, no fixture: tuples are persisted inside event and snapshot JSON, so the
// array shape is a compatibility contract, not just a round trip.
public sealed class ValueTupleJsonConverterFactoryTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new ValueTupleJsonConverterFactory() } };

    [Fact]
    public void A_pair_is_written_as_a_json_array()
    {
        Assert.Equal("""[1,"a"]""", JsonSerializer.Serialize((1, "a"), Options));
    }

    [Fact]
    public void A_tuple_used_as_a_dictionary_key_is_written_as_its_array_text()
    {
        var json = JsonSerializer.Serialize(new Dictionary<(int, string), bool> { [(1, "a")] = true }, Options);

        using var document = JsonDocument.Parse(json);
        Assert.Equal("""[1,"a"]""", Assert.Single(document.RootElement.EnumerateObject()).Name);
    }

    [Fact]
    public void Tuples_of_every_supported_size_round_trip_as_values_and_dictionary_keys()
    {
        AssertRoundTrips(ValueTuple.Create(1));
        AssertRoundTrips((1, "two"));
        AssertRoundTrips((1, "two", 3.5m));
        AssertRoundTrips((1, "two", 3.5m, true));
        AssertRoundTrips((1, "two", 3.5m, true, new DateOnly(2026, 10, 8)));
        AssertRoundTrips((1, "two", 3.5m, true, new DateOnly(2026, 10, 8), 'f'));
        AssertRoundTrips((1, "two", 3.5m, true, new DateOnly(2026, 10, 8), 'f', 7L));
    }

    [Fact]
    public void A_tuple_with_more_than_seven_items_is_rejected()
    {
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize((1, 2, 3, 4, 5, 6, 7, 8), Options));
    }

    private static void AssertRoundTrips<TTuple>(TTuple tuple)
        where TTuple : struct
    {
        Assert.Equal(tuple, JsonSerializer.Deserialize<TTuple>(JsonSerializer.Serialize(tuple, Options), Options));

        var asKey = JsonSerializer.Serialize(new Dictionary<TTuple, int> { [tuple] = 1 }, Options);
        Assert.Equal(1, JsonSerializer.Deserialize<Dictionary<TTuple, int>>(asKey, Options)![tuple]);
    }
}
