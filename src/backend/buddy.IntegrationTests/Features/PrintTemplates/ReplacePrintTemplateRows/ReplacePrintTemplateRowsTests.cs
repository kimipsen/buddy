using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.PrintTemplates;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

using static buddy.IntegrationTests.Features.PrintTemplates.PrintTemplateTestHelpers;

namespace buddy.IntegrationTests.Features.PrintTemplates.ReplacePrintTemplateRows;

[Collection(BuddyApiCollection.Name)]
public sealed class ReplacePrintTemplateRowsTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("ReplacePrintTemplateRows")]
    public async Task The_sample_fridge_sheet_round_trips_in_order()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, family.FirstToken, "Stil");
        var randers = await WorkLocationTestHelpers.AddLocationAsync(fixture, family.SecondToken, "Randers");
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, family.FirstToken, "Familie");
        var template = await CreateAsync(fixture, family.FirstToken);
        var child = family.Child.Id;

        await ReplaceRowsAsync(fixture, family.FirstToken, template.Id,
        [
            new { Kind = Meal, Label = " Aftensmad ", HeightWeight = 1, ChildId = child, MealSlot = Dinner },
            new { Kind = Pickup, Label = "Aflevere / Hente", HeightWeight = 1, ChildId = child },
            new { Kind = WorkLocation, Label = "Far på Stil", HeightWeight = 1, GuardianId = family.FirstId, WorkLocationId = stil.Id },
            new { Kind = WorkLocation, Label = "Mor i Randers", HeightWeight = 1, GuardianId = family.SecondId, WorkLocationId = randers.Id },
            new { Kind = CalendarEvents, Label = "Signes aktiviteter", HeightWeight = 2, CalendarIds = new[] { calendarId }, ShowTime = true, MaxItems = 3 },
            new { Kind = TaskChecklist, Label = "Viggos ansvar", HeightWeight = 2, CalendarIds = new[] { calendarId }, AssignedToId = child },
            new { Kind = CalendarMarker, Label = "Affald der tømmes", HeightWeight = 1, CalendarIds = new[] { calendarId }, TitleFilter = "Skrald" },
            new { Kind = Blank, Label = "Aftaler / Andet", HeightWeight = 3 },
        ]);

        var read = await GetAsync(fixture, family.FirstToken, template.Id);

        Assert.Equal(
            [Meal, Pickup, WorkLocation, WorkLocation, CalendarEvents, TaskChecklist, CalendarMarker, Blank],
            read.Rows.Select(r => r.Kind));
        Assert.Equal("Aftensmad", read.Rows[0].Label);
        Assert.Equal(Dinner, read.Rows[0].MealSlot);
        Assert.Equal(randers.Id, read.Rows[3].WorkLocationId);
        Assert.Equal([calendarId], read.Rows[4].CalendarIds!);
        Assert.True(read.Rows[4].ShowTime);
        Assert.Equal(3, read.Rows[7].HeightWeight);
    }

    [Fact]
    public async Task Saving_the_same_rows_appends_no_event()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await CreateAsync(fixture, token);
        var store = fixture.Host.Services.GetRequiredService<IPrintTemplateEventStore>();

        await ReplaceRowsAsync(fixture, token, template.Id, [BlankRow("Noter")]);
        await ReplaceRowsAsync(fixture, token, template.Id, [BlankRow(" Noter ")]);

        Assert.Equal(2, (await store.ReadAsync(new PrintTemplateId(template.Id), CancellationToken.None)).Count);
    }

    // Each case names the error key it must produce. Asserting the key (not just a 400) matters:
    // the random ids below would also fail the reference checks, which would mask a validator gap.
    public static TheoryData<string, object?[]> StructurallyInvalidRows => new()
    {
        { "rows", [] },
        { "rows", [.. Enumerable.Range(0, 13).Select(_ => BlankRow())] },
        { "rows[0].label", [new { Kind = Pickup, Label = "", HeightWeight = 1, ChildId = Guid.NewGuid() }] },
        // Omitted entirely: rejected while binding the body (RespectRequiredConstructorParameters).
        { "rows[0].label", [new { Kind = Blank, HeightWeight = 1 }] },
        { "rows[0].label", [BlankRow(new string('x', 41))] },
        { "rows[0].heightWeight", [new { Kind = Blank, Label = "", HeightWeight = 0 }] },
        { "rows[0].heightWeight", [new { Kind = Blank, Label = "", HeightWeight = 6 }] },
        { "rows[0].childId", [new { Kind = Meal, Label = "Mad", HeightWeight = 1, ChildId = Guid.NewGuid(), MealGroupId = Guid.NewGuid(), MealSlot = Dinner }] },
        { "rows[0].mealSlot", [new { Kind = Meal, Label = "Mad", HeightWeight = 1, ChildId = Guid.NewGuid() }] },
        { "rows[0].childId", [new { Kind = Pickup, Label = "Hente", HeightWeight = 1 }] },
        { "rows[0].guardianId", [new { Kind = WorkLocation, Label = "Far", HeightWeight = 1 }] },
        { "rows[0].calendarIds", [new { Kind = CalendarEvents, Label = "Ting", HeightWeight = 1, CalendarIds = Array.Empty<Guid>() }] },
        { "rows[0].calendarIds", [new { Kind = CalendarMarker, Label = "X", HeightWeight = 1, CalendarIds = new[] { Guid.Empty, Guid.Empty } }] },
        { "rows[0].calendarIds", [new { Kind = CalendarMarker, Label = "X", HeightWeight = 1, CalendarIds = new Guid?[] { null } }] },
        { "rows[0].maxItems", [new { Kind = TaskChecklist, Label = "X", HeightWeight = 1, CalendarIds = new[] { Guid.NewGuid() }, MaxItems = 9 }] },
        { "rows[0].kind", [new { Kind = Blank, Label = "", HeightWeight = 1, ShowTime = true }] },
        { "rows[0].kind", [new { Kind = Pickup, Label = "Hente", HeightWeight = 1, ChildId = Guid.NewGuid(), CalendarIds = new[] { Guid.NewGuid() } }] },
        { "rows[0].kind", [new { Kind = 42, Label = "?", HeightWeight = 1 }] },
        { "", [null] },
    };

    [Theory]
    [MemberData(nameof(StructurallyInvalidRows))]
    public async Task Structurally_invalid_rows_are_rejected_on_the_offending_field(string expectedKey, object?[] rows)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await CreateAsync(fixture, token);

        var response = await ReplaceRowsAsync(fixture, token, template.Id, rows!, expectedStatus: 400);

        Assert.Contains(expectedKey, response.ReadAsJson<ErrorEnvelope>().Details.Keys);
    }

    [Fact]
    public async Task A_blank_row_may_have_an_empty_label()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await CreateAsync(fixture, token);

        await ReplaceRowsAsync(fixture, token, template.Id, [new { Kind = Blank, Label = "", HeightWeight = 1 }]);

        Assert.Equal("", Assert.Single((await GetAsync(fixture, token, template.Id)).Rows).Label);
    }

    [Fact]
    public async Task Another_group_member_can_edit_rows_that_reference_what_only_the_first_member_can_see()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, ownerToken, "Alex");
        var privateCalendar = await CalendarTestHelpers.CreateCalendarAsync(fixture, ownerToken, "Privat");
        var (member, memberToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Familien");
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, memberToken, member.Email, GroupRole.Member);
        var template = await CreateAsync(fixture, ownerToken, "Fælles", groupId);

        object ownersCalendarRow = new { Kind = CalendarEvents, Label = "Aftaler", HeightWeight = 1, CalendarIds = new[] { privateCalendar } };
        object ownersPickupRow = new { Kind = Pickup, Label = "Hente", HeightWeight = 1, ChildId = child.Id };
        await ReplaceRowsAsync(fixture, ownerToken, template.Id, [ownersCalendarRow, ownersPickupRow]);

        // The member can't view that calendar or guard that child, but keeping the owner's rows while
        // adding their own is fine -- only new references are checked.
        await ReplaceRowsAsync(fixture, memberToken, template.Id, [ownersCalendarRow, ownersPickupRow, BlankRow("Noter")]);

        // Adding a new reference the member can't reach still fails.
        await ReplaceRowsAsync(fixture, memberToken, template.Id,
            [ownersCalendarRow, ownersPickupRow, new { Kind = CalendarMarker, Label = "X", HeightWeight = 1, CalendarIds = new[] { Guid.NewGuid() } }],
            expectedStatus: 400);

        Assert.Equal(3, (await GetAsync(fixture, ownerToken, template.Id)).Rows.Count);
    }

    [Fact]
    public async Task A_row_whose_work_location_was_archived_since_does_not_block_later_saves()
    {
        var (_, token, guardianId) = await fixture.CreateAuthenticatedUserAsync();
        var stil = await WorkLocationTestHelpers.AddLocationAsync(fixture, token, "Stil");
        var template = await CreateAsync(fixture, token);
        object row = new { Kind = WorkLocation, Label = "Far på Stil", HeightWeight = 1, GuardianId = guardianId, WorkLocationId = stil.Id };
        await ReplaceRowsAsync(fixture, token, template.Id, [row]);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/work-locations/me/locations/{stil.Id}");
            _.StatusCodeShouldBe(204);
        });

        await ReplaceRowsAsync(fixture, token, template.Id, [row, BlankRow()]);
    }

    [Fact]
    public async Task A_child_the_caller_does_not_guard_is_rejected()
    {
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var othersChild = await GuardianTestHelpers.CreateChildAsync(fixture, otherToken, "Sam");
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await CreateAsync(fixture, token);

        await ReplaceRowsAsync(fixture, token, template.Id,
            [new { Kind = Pickup, Label = "Hente", HeightWeight = 1, ChildId = othersChild.Id }], expectedStatus: 400);
    }

    [Fact]
    public async Task A_calendar_the_caller_cannot_view_is_rejected()
    {
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var privateCalendar = await CalendarTestHelpers.CreateCalendarAsync(fixture, otherToken, "Privat");
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await CreateAsync(fixture, token);

        await ReplaceRowsAsync(fixture, token, template.Id,
            [new { Kind = CalendarMarker, Label = "X", HeightWeight = 1, CalendarIds = new[] { privateCalendar } }], expectedStatus: 400);
    }

    [Fact]
    public async Task A_work_location_row_for_a_guardian_who_is_not_a_co_guardian_is_rejected()
    {
        var (_, strangerToken, strangerId) = await fixture.CreateAuthenticatedUserAsync();
        await WorkLocationTestHelpers.AddLocationAsync(fixture, strangerToken, "Elsewhere");
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        await GuardianTestHelpers.CreateChildAsync(fixture, token, "Alex");
        var template = await CreateAsync(fixture, token);

        await ReplaceRowsAsync(fixture, token, template.Id,
            [new { Kind = WorkLocation, Label = "Them", HeightWeight = 1, GuardianId = strangerId }], expectedStatus: 400);
    }

    [Fact]
    public async Task An_archived_or_unknown_work_location_is_rejected_but_a_guardian_wide_row_is_fine()
    {
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var gone = await WorkLocationTestHelpers.AddLocationAsync(fixture, family.SecondToken, "Gone");

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Delete.Url($"/work-locations/me/locations/{gone.Id}");
            _.StatusCodeShouldBe(204);
        });

        var template = await CreateAsync(fixture, family.FirstToken);

        foreach (var locationId in new[] { gone.Id, Guid.NewGuid() })
        {
            await ReplaceRowsAsync(fixture, family.FirstToken, template.Id,
                [new { Kind = WorkLocation, Label = "Mor", HeightWeight = 1, GuardianId = family.SecondId, WorkLocationId = locationId }],
                expectedStatus: 400);
        }

        await ReplaceRowsAsync(fixture, family.FirstToken, template.Id,
            [new { Kind = WorkLocation, Label = "Mor", HeightWeight = 1, GuardianId = family.SecondId }]);
    }

    [Fact]
    public async Task A_stranger_gets_not_found()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var template = await CreateAsync(fixture, ownerToken);
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();

        await ReplaceRowsAsync(fixture, strangerToken, template.Id, [BlankRow()], expectedStatus: 404);
    }
}
