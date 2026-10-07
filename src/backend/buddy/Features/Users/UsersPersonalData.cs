using buddy.Common.Erasure;

using Marten;

namespace buddy.Features.Users;

// What erasing a user masks in their own stream (IUserEventStore.EraseAsync): every field that
// identifies them. The stream itself stays, so a UserId still resolves -- to an erased, deleted
// user. See gdpr-data-protection.md.
public static class UsersPersonalData
{
    private static readonly Name ErasedName = Name.New(Erased.Text, Erased.Text);

    public static void ConfigureMasking(StoreOptions options)
    {
        options.Events.AddMaskingRuleForProtectedInformation<UserCreated>(e => e with
        {
            KeycloakSubject = KeycloakSubject.New(Erased.Text),
            Email = e.Email with { Value = Erased.Text },
            UserName = Erased.Text,
            Name = ErasedName,
        });
        options.Events.AddMaskingRuleForProtectedInformation<NameUpdated>(e => e with { Before = ErasedName, After = ErasedName });
        options.Events.AddMaskingRuleForProtectedInformation<EmailUpdated>(e => e with
        {
            Before = e.Before with { Value = Erased.Text },
            After = e.After with { Value = Erased.Text },
        });
    }
}
