using buddy.Features.Calendars;
using buddy.Features.Users;

using Xunit;

// buddy.Email (the mail-sending namespace) shadows buddy.Features.Users.Email (this type) under
// unqualified lookup, because this test project's own root namespace is also "buddy" -- enclosing
// namespace members are checked before this file's using-alias, so "Email" alone still resolves
// to the namespace. Renaming the alias sidesteps the collision instead of fighting it.
using UserEmail = buddy.Features.Users.Email;

namespace buddy.IntegrationTests.EventShapeTests;

public sealed class UserEventShapeTests
{
    private static readonly UserId FixedUserId = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    private static readonly DateTimeOffset FixedInstant = new(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UserCreated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new UserCreated(
            FixedUserId,
            KeycloakSubject.New("keycloak-subject-123"),
            UserEmail.Unverified("alice@buddy.test"),
            "alice",
            Name.New("Alice", "Anderson"),
            FixedInstant),
        "Users/UserCreated.json");

    [Fact]
    public void UserDeleted() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new UserDeleted(FixedUserId, FixedInstant),
        "Users/UserDeleted.json");

    [Fact]
    public void NameUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new NameUpdated(FixedUserId, Name.New("Alice", "Anderson"), Name.New("Ally", "Anderson"), FixedInstant),
        "Users/NameUpdated.json");

    [Fact]
    public void EmailUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new EmailUpdated(FixedUserId, UserEmail.Verified("alice@buddy.test"), UserEmail.Unverified("alice2@buddy.test"), FixedInstant),
        "Users/EmailUpdated.json");

    [Fact]
    public void EmailVerificationRequested() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new EmailVerificationRequested(FixedUserId, "sha256-hash-of-token", FixedInstant.AddHours(1), FixedInstant),
        "Users/EmailVerificationRequested.json");

    [Fact]
    public void EmailVerified() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new EmailVerified(FixedUserId, FixedInstant),
        "Users/EmailVerified.json");

    [Fact]
    public void TimeZoneUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new TimeZoneUpdated(FixedUserId, TimeZoneId.New("UTC"), TimeZoneId.New("Europe/Copenhagen"), FixedInstant),
        "Users/TimeZoneUpdated.json");

    [Fact]
    public void LanguageUpdated() => EventShapeTestSupport.AssertMatchesGoldenFile(
        new LanguageUpdated(FixedUserId, Language.New("en"), Language.New("da"), FixedInstant),
        "Users/LanguageUpdated.json");
}
