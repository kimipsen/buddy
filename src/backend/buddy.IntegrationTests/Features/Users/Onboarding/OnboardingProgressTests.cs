using buddy.Common;
using buddy.Common.Concurrency;
using buddy.Common.Erasure;
using buddy.Features.Groups;
using buddy.Features.Users;
using buddy.IntegrationTests.Features.Groups;
using buddy.IntegrationTests.Fixtures;
using buddy.IntegrationTests.Meta;

using JasperFx;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace buddy.IntegrationTests.Features.Users.Onboarding;

[Collection(BuddyApiCollection.Name)]
public sealed class OnboardingProgressTests(BuddyApiFixture fixture)
{
    [Fact]
    [CoversEndpoint("GetOnboardingProgress")]
    public async Task A_user_who_never_started_the_guide_gets_not_started_at_version_zero()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var progress = await OnboardingTestHelpers.GetAsync(fixture, token);

        Assert.Equal(new OnboardingProgressDto(OnboardingStatus.NotStarted, null, false, 0), progress);
    }

    [Fact]
    [CoversEndpoint("UpdateOnboardingProgress")]
    public async Task Starting_and_advancing_the_guide_bumps_the_version_and_reads_back()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Setup family");

        var started = await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Active, null, false, version: 0);
        Assert.Equal(new OnboardingProgressDto(OnboardingStatus.Active, null, false, 1), started);

        var withGroup = await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Active, groupId, true, version: 1);
        Assert.Equal(new OnboardingProgressDto(OnboardingStatus.Active, groupId, true, 2), withGroup);

        Assert.Equal(withGroup, await OnboardingTestHelpers.GetAsync(fixture, token));
    }

    [Fact]
    public async Task An_unchanged_write_does_not_bump_the_version()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Deferred, null, false, version: 0);

        var again = await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Deferred, null, false, version: 1);

        Assert.Equal(1, again.Version);
    }

    [Fact]
    public async Task Progress_is_per_user()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, otherToken, _) = await fixture.CreateAuthenticatedUserAsync();
        await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Active, null, false, version: 0);

        Assert.Equal(OnboardingStatus.NotStarted, (await OnboardingTestHelpers.GetAsync(fixture, otherToken)).Status);
    }

    [Fact]
    public async Task A_stale_version_is_a_concurrency_conflict()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Active, null, false, version: 0);
        await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Deferred, null, false, version: 1);

        // A second tab that read version 1 tries to write on top of version 2.
        var response = await OnboardingTestHelpers.PutRawAsync(fixture, token, OnboardingStatus.Active, null, true, version: 1, expectedStatus: 409);

        Assert.Equal(ConcurrencyConflictMiddleware.ErrorCode, response.ReadAsJson<ErrorEnvelope>().Code);
        Assert.Equal(OnboardingStatus.Deferred, (await OnboardingTestHelpers.GetAsync(fixture, token)).Status);
    }

    [Fact]
    public async Task Creating_again_from_version_zero_is_a_concurrency_conflict()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Active, null, false, version: 0);

        var response = await OnboardingTestHelpers.PutRawAsync(fixture, token, OnboardingStatus.Deferred, null, false, version: 0, expectedStatus: 409);

        Assert.Equal(ConcurrencyConflictMiddleware.ErrorCode, response.ReadAsJson<ErrorEnvelope>().Code);
    }

    [Fact]
    public async Task Two_writers_racing_from_the_same_version_leave_one_conflict()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        var read = await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Active, null, false, version: 0);
        var store = fixture.Host.Services.GetRequiredService<IOnboardingProgressStore>();
        var progress = new OnboardingProgress(new UserId(userId), OnboardingStatus.Active, null, false, read.Version);

        await store.SaveAsync(progress with { Status = OnboardingStatus.Deferred }, CancellationToken.None);

        await Assert.ThrowsAnyAsync<ConcurrencyException>(() =>
            store.SaveAsync(progress with { InvitationsSkipped = true }, CancellationToken.None));
    }

    [Fact]
    public async Task Two_first_writes_racing_leave_one_conflict()
    {
        var (_, _, userId) = await fixture.CreateAuthenticatedUserAsync();
        var store = fixture.Host.Services.GetRequiredService<IOnboardingProgressStore>();
        var progress = OnboardingProgress.NotStarted(new UserId(userId)) with { Status = OnboardingStatus.Active };

        await store.SaveAsync(progress, CancellationToken.None);

        await Assert.ThrowsAnyAsync<ConcurrencyException>(() => store.SaveAsync(progress, CancellationToken.None));
    }

    [Fact]
    public async Task A_group_the_caller_does_not_belong_to_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var (_, strangerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var strangersGroup = await GroupTestHelpers.CreateGroupAsync(fixture, strangerToken, "Not yours");

        await OnboardingTestHelpers.PutRawAsync(fixture, token, OnboardingStatus.Active, strangersGroup, false, version: 0, expectedStatus: 400);

        Assert.Equal(OnboardingStatus.NotStarted, (await OnboardingTestHelpers.GetAsync(fixture, token)).Status);
    }

    [Fact]
    public async Task A_group_the_caller_is_only_a_member_of_is_rejected()
    {
        var (_, ownerToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var (member, memberToken, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, ownerToken, "Members only");
        await GroupTestHelpers.AddMemberAsync(fixture, ownerToken, groupId, memberToken, member.Email, GroupRole.Member);

        await OnboardingTestHelpers.PutRawAsync(fixture, memberToken, OnboardingStatus.Active, groupId, false, version: 0, expectedStatus: 400);
    }

    [Fact]
    public async Task An_unknown_group_is_rejected()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await OnboardingTestHelpers.PutRawAsync(fixture, token, OnboardingStatus.Active, Guid.CreateVersion7(), false, version: 0, expectedStatus: 400);
    }

    [Theory]
    [InlineData(OnboardingStatus.NotStarted)]
    [InlineData((OnboardingStatus)42)]
    public async Task Not_started_and_unknown_statuses_are_rejected(OnboardingStatus status)
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        var response = await OnboardingTestHelpers.PutRawAsync(fixture, token, status, null, false, version: 0, expectedStatus: 400);

        Assert.Contains("Status", response.ReadAsJson<ErrorEnvelope>().Details.Keys);
    }

    [Fact]
    public async Task Completing_needs_a_setup_group()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();

        await OnboardingTestHelpers.PutRawAsync(fixture, token, OnboardingStatus.Completed, null, false, version: 0, expectedStatus: 400);
    }

    [Fact]
    public async Task A_completed_guide_cannot_be_reopened()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Done");
        await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Completed, groupId, false, version: 0);

        await OnboardingTestHelpers.PutRawAsync(fixture, token, OnboardingStatus.Active, groupId, false, version: 1, expectedStatus: 400);

        // Repeating the completion itself is an idempotent no-op.
        var again = await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Completed, groupId, false, version: 1);
        Assert.Equal(1, again.Version);
    }

    [Fact]
    public async Task The_setup_group_cannot_be_cleared()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Keep me");
        await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Active, groupId, false, version: 0);

        await OnboardingTestHelpers.PutRawAsync(fixture, token, OnboardingStatus.Active, null, false, version: 1, expectedStatus: 400);
    }

    [Fact]
    public async Task A_guide_whose_group_was_deleted_can_still_be_deferred()
    {
        var (_, token, _) = await fixture.CreateAuthenticatedUserAsync();
        var groupId = await GroupTestHelpers.CreateGroupAsync(fixture, token, "Short-lived");
        await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Active, groupId, false, version: 0);

        await fixture.Host.Scenario(_ =>
        {
            _.WithRequestHeader("Authorization", $"Bearer {token}");
            _.Delete.Url($"/groups/{groupId}");
            _.StatusCodeShouldBe(204);
        });

        var deferred = await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Deferred, groupId, false, version: 1);
        Assert.Equal(OnboardingStatus.Deferred, deferred.Status);
    }

    [Fact]
    public async Task Erasing_the_guardian_deletes_their_progress()
    {
        var (_, token, userId) = await fixture.CreateAuthenticatedUserAsync();
        await OnboardingTestHelpers.PutAsync(fixture, token, OnboardingStatus.Active, null, false, version: 0);
        var eraser = fixture.Host.Services.GetServices<IPersonalDataEraser>().OfType<OnboardingPersonalDataEraser>().Single();

        await eraser.EraseGuardianAsync(new ErasureSubject(new UserId(userId), null), CancellationToken.None);

        var store = fixture.Host.Services.GetRequiredService<IOnboardingProgressStore>();
        Assert.Null(await store.FindAsync(new UserId(userId), CancellationToken.None));
    }
}
