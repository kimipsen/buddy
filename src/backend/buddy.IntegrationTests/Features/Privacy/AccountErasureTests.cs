using Alba;

using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Privacy;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Babysitters;
using buddy.IntegrationTests.Features.Calendars;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Features.Guardians;
using buddy.IntegrationTests.Features.Mealplans;
using buddy.IntegrationTests.Features.Medicines;
using buddy.IntegrationTests.Features.PrintTemplates;
using buddy.IntegrationTests.Features.SleepDiaries;
using buddy.IntegrationTests.Features.WorkLocations;
using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Privacy;

// DELETE /users/me erases the person and cascades -- docs/backend/analysis/gdpr-data-protection.md,
// Questions 2-4. Every personal value is unique to its test, and PersonalDataScanner checks the
// database itself afterwards.
[Collection(BuddyApiCollection.Name)]
public sealed class AccountErasureTests(BuddyApiFixture fixture)
{
    private static readonly DateOnly Night = new(2026, 3, 2);

    [Fact]
    public async Task Deleting_an_account_erases_the_guardian_and_the_child_only_they_guard()
    {
        var n = Unique();
        var guardian = await fixture.CreateUserAsync(givenName: $"Gerda{n}", familyName: $"Gauss{n}");
        var token = await fixture.GetAccessTokenAsync(guardian);
        await fixture.GetUserIdAsync(token);

        var child = await GuardianTestHelpers.CreateChildAsync(fixture, token, $"Cosmo{n}", $"Crane{n}", $"child.{n}");

        await SleepDiaryTestHelpers.LogAsync(fixture, token, child.Id, Night, SleepDiaryTestHelpers.FullNight($"nightmare{n}"));
        await SleepDiaryTestHelpers.UpdateNotesAsync(fixture, token, child.Id, $"hygiene{n}");
        await SleepDiaryTestHelpers.CreateShareLinkAsync(fixture, token, child.Id);
        await MedicineTestHelpers.CreateMedicineScheduleAsync(fixture, token, child.Id, new CreateMedicineScheduleOptions(Name: $"Medizor{n}", Dosage: $"dose{n}"));
        await BabysitterTestHelpers.AddAsync(fixture, token, $"Sitter{n}", $"sitter{n}@example.test");
        await WorkLocationTestHelpers.AddLocationAsync(fixture, token, $"Office{n}");
        await PrintTemplateTestHelpers.CreateAsync(fixture, token, $"Template{n}");
        await MealplanTestHelpers.CreateMealAsync(fixture, token, child.Id, new CreateMealOptions(Name: $"Meal{n}", Description: $"recipe{n}"));
        await GuardianTestHelpers.InviteGuardianAsync(fixture, token, child.Id, $"invitee{n}@example.test", GuardianKind.Parent);

        // A group only they are in, with a calendar and a task for the child.
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, $"Group{n}");
        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Put.Url($"/groups/{groupId}/children/{child.Id}");
            _.StatusCodeShouldBe(204);
        });
        var calendarId = await CalendarTestHelpers.CreateCalendarAsync(fixture, token, $"Calendar{n}", groupId);
        await CalendarTestHelpers.CreateTaskAsync(fixture, token, calendarId, title: $"Task{n}", assignedTo: child.Id);

        await DeleteAccountAsync(token, expectedStatus: 204);

        var leftovers = await PersonalDataScanner.FindAsync(
            fixture,
            $"Gerda{n}", $"Gauss{n}", guardian.Email, guardian.Username,
            $"Cosmo{n}", $"Crane{n}", child.Username,
            $"nightmare{n}", $"hygiene{n}", $"Medizor{n}", $"dose{n}",
            $"Sitter{n}", $"sitter{n}@example.test", $"Office{n}", $"Template{n}", $"Meal{n}", $"recipe{n}",
            $"invitee{n}@example.test", $"Group{n}", $"Calendar{n}", $"Task{n}");

        Assert.True(leftovers.Count == 0, $"Personal data left after erasure:\n{string.Join("\n", leftovers)}");
        Assert.False(await fixture.KeycloakUserExistsAsync(guardian.Username));
        Assert.False(await fixture.KeycloakUserExistsAsync(child.Username));

        var users = fixture.Host.Services.GetRequiredService<IUserEventStore>();
        Assert.True((await users.FindSnapshotAsync(new UserId(child.Id), CancellationToken.None))?.IsErased);
    }

    [Fact]
    public async Task A_co_guarded_child_and_its_data_stay_with_the_other_guardian()
    {
        var n = Unique();
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        await SleepDiaryTestHelpers.LogAsync(fixture, family.FirstToken, family.Child.Id, Night, SleepDiaryTestHelpers.FullNight($"kept{n}"));

        await DeleteAccountAsync(family.FirstToken, expectedStatus: 204);

        var diary = await SleepDiaryTestHelpers.ListAsync(fixture, family.SecondToken, family.Child.Id, Night, Night);
        Assert.Equal($"kept{n}", Assert.Single(diary!.Entries).Remarks);
        Assert.True(await fixture.KeycloakUserExistsAsync(family.Child.Username));
    }

    [Fact]
    public async Task A_group_passes_to_its_longest_standing_admin()
    {
        var (owner, ownerToken, ownerId) = await fixture.CreateAuthenticatedUserAsync();
        var (member, memberToken, memberId) = await fixture.CreateAuthenticatedUserAsync();
        var (firstAdmin, firstAdminToken, firstAdminId) = await fixture.CreateAuthenticatedUserAsync();
        var (secondAdmin, secondAdminToken, _) = await fixture.CreateAuthenticatedUserAsync();

        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Shared family");
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, memberToken, member.Email, GroupRole.Member);
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, firstAdminToken, firstAdmin.Email, GroupRole.Admin);
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, secondAdminToken, secondAdmin.Email, GroupRole.Admin);

        await DeleteAccountAsync(ownerToken, expectedStatus: 204);

        var group = await GroupTestHelpers.GetGroupAsync(fixture, firstAdminToken, groupId);
        Assert.Equal(GroupRole.Owner, Assert.Single(group.Members, m => m.UserId == firstAdminId).Role);
        Assert.Equal(GroupRole.Member, Assert.Single(group.Members, m => m.UserId == memberId).Role);
        Assert.DoesNotContain(group.Members, m => m.UserId == ownerId);
    }

    [Fact]
    public async Task Family_data_anchored_to_an_erased_child_passes_to_a_sibling()
    {
        var n = Unique();
        var family = await WorkLocationTestHelpers.CreateCoGuardiansAsync(fixture);
        var onlyTheirs = await GuardianTestHelpers.CreateChildAsync(fixture, family.FirstToken, $"Orphan{n}", "Child");
        await MealplanTestHelpers.CreateMealAsync(fixture, family.FirstToken, onlyTheirs.Id, new CreateMealOptions(Name: $"FamilyMeal{n}"));

        await DeleteAccountAsync(family.FirstToken, expectedStatus: 204);

        var meals = await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {family.SecondToken}");
            _.Get.Url($"/mealplans/children/{family.Child.Id}/meals");
            _.StatusCodeShouldBe(200);
        });

        Assert.Contains($"FamilyMeal{n}", await meals.ReadAsTextAsync());
        Assert.False(await fixture.KeycloakUserExistsAsync(onlyTheirs.Username));
    }

    [Fact]
    public async Task A_child_cannot_delete_its_own_account()
    {
        var (_, guardianToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var child = await GuardianTestHelpers.CreateChildAsync(fixture, guardianToken);
        var childToken = await GuardianTestHelpers.CompleteChildLoginAsync(fixture, child);

        await DeleteAccountAsync(childToken, expectedStatus: 403);

        Assert.True(await fixture.KeycloakUserExistsAsync(child.Username));
    }

    [Fact]
    public async Task The_sweep_finishes_a_deletion_that_was_never_erased()
    {
        var n = Unique();
        var user = await fixture.CreateUserAsync(givenName: $"Legacy{n}");
        var token = await fixture.GetAccessTokenAsync(user);
        var userId = new UserId(await fixture.GetUserIdAsync(token));

        // As DELETE /users/me left it before erasure existed: UserDeleted and nothing else.
        var users = fixture.Host.Services.GetRequiredService<IUserEventStore>();
        await users.AppendAsync(userId, [new UserDeleted(userId, DateTimeOffset.UtcNow)], CancellationToken.None);

        await FinishUnfinishedErasuresAsync();

        Assert.True((await users.FindSnapshotAsync(userId, CancellationToken.None))?.IsErased);
        Assert.False(await fixture.KeycloakUserExistsAsync(user.Username));
        Assert.Empty(await PersonalDataScanner.FindAsync(fixture, $"Legacy{n}", user.Email));
    }

    [Fact]
    public async Task The_sweep_erases_again_a_user_a_restore_brought_back()
    {
        var n = Unique();
        var user = await fixture.CreateUserAsync(givenName: $"Restored{n}");
        var token = await fixture.GetAccessTokenAsync(user);
        var userId = new UserId(await fixture.GetUserIdAsync(token));

        // As after restoring a backup taken before the erasure: on the re-imported ledger, but the
        // restored data has the user as they were.
        var users = fixture.Host.Services.GetRequiredService<IUserEventStore>();
        await users.RecordErasureAsync(userId, CancellationToken.None);

        await FinishUnfinishedErasuresAsync();

        Assert.True((await users.FindSnapshotAsync(userId, CancellationToken.None))?.IsErased);
        Assert.Empty(await PersonalDataScanner.FindAsync(fixture, $"Restored{n}", user.Email));

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Get.Url("/users/me/children");
            _.StatusCodeShouldBe(403);
        });
    }

    [Fact]
    public async Task The_sweep_erases_a_deleted_user_whose_events_are_gone_but_whose_snapshot_stayed()
    {
        var n = Unique();
        var user = await fixture.CreateUserAsync(givenName: $"Orphan{n}");
        var token = await fixture.GetAccessTokenAsync(user);
        var userId = new UserId(await fixture.GetUserIdAsync(token));

        var users = fixture.Host.Services.GetRequiredService<IUserEventStore>();
        await users.AppendAsync(userId, [new UserDeleted(userId, DateTimeOffset.UtcNow)], CancellationToken.None);

        // As after the users event tables were wiped or partially restored while the snapshots
        // schema stayed: a deleted, not yet erased snapshot with no stream behind it.
        var store = fixture.Host.Services.GetRequiredService<IUsersStore>();
        await store.Advanced.Clean.DeleteSingleEventStreamAsync(userId.Value, ct: CancellationToken.None);

        await FinishUnfinishedErasuresAsync();

        Assert.Null(await users.FindSnapshotAsync(userId, CancellationToken.None));
        Assert.DoesNotContain(userId, await users.ListUnfinishedErasuresAsync(CancellationToken.None));
        Assert.Empty(await PersonalDataScanner.FindAsync(fixture, $"Orphan{n}", user.Email));
    }

    private async Task FinishUnfinishedErasuresAsync()
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<UserErasure>().FinishUnfinishedAsync(CancellationToken.None);
    }

    private Task<IScenarioResult> DeleteAccountAsync(string token, int expectedStatus) =>
        fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url("/users/me");
            _.StatusCodeShouldBe(expectedStatus);
        });

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];
}
