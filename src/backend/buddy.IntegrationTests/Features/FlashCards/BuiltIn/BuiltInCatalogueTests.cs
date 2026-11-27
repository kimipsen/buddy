using System.Security.Cryptography;
using System.Text;

using buddy.Features.FlashCards;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.FlashCards.BuiltIn;

// The shipped catalogue and the catalogue mechanics, with no host: plain unit tests.
public sealed class BuiltInCatalogueTests
{
    // The UUIDv5 namespace of the built-in set ids. It was chosen once and must NEVER change: every id
    // below, and every enabled set and star history that refers to one, is derived from it.
    private static readonly Guid Namespace = new("4f3c2a1e-9b7d-5e60-8a14-2c5d7e9f1b03");

    private static readonly (string Name, int Levels)[] Subjects = [("clock", 4), ("sight", 4), ("letters", 2), ("english", 3), ("countries", 3)];

    // key -> (Danish display name). The slice 6 e2e addresses two of these by name, so a rename must fail here.
    private static readonly (string Key, string Name)[] ExpectedNames =
    [
        ("clock:1", "Klokken: hele timer"),
        ("clock:2", "Klokken: halve timer"),
        ("clock:3", "Klokken: kvarter"),
        ("clock:4", "Klokken: hver 5. minut"),
        ("sight:1", "Sight words: 0. klasse"),
        ("sight:2", "Sight words: 1. klasse"),
        ("sight:3", "Sight words: 2. klasse"),
        ("sight:4", "Sight words: svære ord"),
        ("letters:1", "Bogstaver: store"),
        ("letters:2", "Bogstaver: små"),
        ("english:1", "Engelsk: niveau 1"),
        ("english:2", "Engelsk: niveau 2"),
        ("english:3", "Engelsk: niveau 3"),
        ("countries:1", "Lande: Norden"),
        ("countries:2", "Lande: Europa"),
        ("countries:3", "Lande: Verden")
    ];

    // RFC 9562 UUIDv5: SHA-1 over the namespace bytes (big-endian) followed by the name.
    private static Guid Uuid5(string name)
    {
        var ns = Namespace.ToByteArray(bigEndian: true);
#pragma warning disable CA5350 // RFC 9562 defines UUIDv5 as SHA-1; this derives an identifier, not a security value.
        var hash = SHA1.HashData([.. ns, .. Encoding.UTF8.GetBytes(name)]);
#pragma warning restore CA5350
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }

    [Fact]
    public void The_sixteen_ids_are_the_uuid_v5_of_their_subject_and_level_and_never_change()
    {
        var expected = Subjects.SelectMany(s => Enumerable.Range(1, s.Levels).Select(level => (Key: $"{s.Name}:{level}", Id: Uuid5($"builtin:{s.Name}:{level}")))).ToArray();

        Assert.Equal(16, BuiltInCatalogue.Default.All.Count);
        Assert.Equal(expected.Select(e => e.Key), BuiltInCatalogue.Default.All.Select(s => s.BuiltIn!.Key));
        Assert.Equal(expected.Select(e => e.Id), BuiltInCatalogue.Default.All.Select(s => s.Id.Value));
    }

    [Fact]
    public void The_sixteen_keys_have_their_pinned_danish_names()
    {
        Assert.Equal(ExpectedNames, BuiltInCatalogue.Default.All.Select(s => (s.BuiltIn!.Key, s.Name)));
    }

    [Fact]
    public void Every_default_set_is_built_in_unowned_by_a_real_user_and_not_deleted()
    {
        Assert.All(BuiltInCatalogue.Default.All, set =>
        {
            Assert.Equal(FlashCardSetKind.BuiltIn, set.Kind);
            Assert.Equal(BuiltInCatalogue.SystemOwnerId, set.OwnerId);
            Assert.False(set.IsDeleted);
            Assert.Null(set.Math);
            Assert.Empty(set.Cards);
            Assert.Same(set, BuiltInCatalogue.Default.TryFind(set.Id));
        });
        Assert.Equal(16, BuiltInCatalogue.Default.All.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void No_card_key_is_retired_at_ship()
    {
        Assert.Empty(BuiltInCatalogue.BuiltInRetiredKeys);
    }

    [Fact]
    public void Production_registers_the_real_catalogue_as_the_singleton()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused" })
            .Build();
        var services = new ServiceCollection().AddLogging().AddFlashCardsFeature(configuration);
        using var provider = services.BuildServiceProvider();

        var catalogue = provider.GetRequiredService<IBuiltInCatalogue>();

        Assert.Same(BuiltInCatalogue.Default, catalogue);
        Assert.Equal(16, catalogue.All.Count);
    }

    [Fact]
    public void A_synthesised_set_holds_the_key_card_count_and_first_card_as_its_example()
    {
        var set = FixtureBuiltInCatalogue.Instance.TryFind(FixtureBuiltInCatalogue.ClockId)!;

        Assert.Equal("fixture:clock", set.BuiltIn!.Key);
        Assert.Equal(3, set.UniqueCardCount);
        Assert.Equal("clock:3:30", set.BuiltIn.Example!.Key);
        Assert.Null(FixtureBuiltInCatalogue.Instance.TryFind(FixtureBuiltInCatalogue.EmptyId)!.BuiltIn!.Example);
        Assert.Equal(0, FixtureBuiltInCatalogue.Instance.TryFind(FixtureBuiltInCatalogue.EmptyId)!.UniqueCardCount);
        Assert.Null(FixtureBuiltInCatalogue.Instance.TryFind(FlashCardSetId.New()));
    }

    [Fact]
    public void A_card_key_resolves_to_found_retired_or_unknown()
    {
        var catalogue = FixtureBuiltInCatalogue.Instance;
        var clock = catalogue.TryFind(FixtureBuiltInCatalogue.ClockId)!;

        var found = catalogue.TryGetCard(clock, "clock:6:00");
        Assert.Equal(BuiltInCardLookupKind.Found, found.Kind);
        Assert.Equal("klokken seks (6:00)", found.Card!.Answer);
        Assert.Equal(BuiltInCardLookupKind.Retired, catalogue.TryGetCard(clock, FixtureBuiltInCatalogue.RetiredKey).Kind);
        Assert.Equal(BuiltInCardLookupKind.Unknown, catalogue.TryGetCard(clock, "never:existed").Kind);
        // A valid key of another set is unknown here, never found and never echoed.
        Assert.Equal(BuiltInCardLookupKind.Unknown, catalogue.TryGetCard(clock, "flag:DK").Kind);
        // Exact match only: no case folding, no trimming.
        Assert.Equal(BuiltInCardLookupKind.Unknown, catalogue.TryGetCard(clock, "CLOCK:3:30").Kind);
        Assert.Equal(BuiltInCardLookupKind.Unknown, catalogue.TryGetCard(clock, " clock:3:30").Kind);
    }

    [Fact]
    public void A_set_that_is_not_in_the_catalogue_has_only_unknown_cards()
    {
        var stored = new FlashCardSet(FlashCardSetId.New(), new(Guid.NewGuid()), "x", FlashCardSetKind.Text, null, [], false, null);

        Assert.Equal(BuiltInCardLookupKind.Unknown, FixtureBuiltInCatalogue.Instance.TryGetCard(stored, "clock:3:30").Kind);
    }

    [Fact]
    public void A_deck_has_exactly_the_requested_size_even_from_a_smaller_set_and_a_seed_makes_it_repeatable()
    {
        var catalogue = FixtureBuiltInCatalogue.Instance;
        var clock = catalogue.TryFind(FixtureBuiltInCatalogue.ClockId)!;

        var first = catalogue.Draw(clock, 10, new Random(42));
        var second = catalogue.Draw(clock, 10, new Random(42));

        Assert.Equal(10, first.Count);
        Assert.Equal(first.Select(c => c.Key), second.Select(c => c.Key));
        Assert.Equal(3, first.Select(c => c.Key).Distinct().Count());
        Assert.Equal(15, catalogue.Draw(catalogue.TryFind(FixtureBuiltInCatalogue.FlagsId)!, 15, new Random(1)).Count);
    }
}
