using System.Collections.Immutable;

using FluentValidation;

namespace buddy.Features.Groups;

// The three Update*PermissionPolicy validators: every key and value of the policy is a defined
// enum member. The JSON binder still accepts a number for an enum (see Program.cs), so an
// out-of-range one would otherwise be stored.
public static class PolicyRules
{
    public static IRuleBuilderOptions<T, ImmutableDictionary<GroupRole, TTier>> DefinedRolesAndTiers<T, TTier>(
        this IRuleBuilder<T, ImmutableDictionary<GroupRole, TTier>> rule)
        where TTier : struct, Enum =>
        rule.Must(policy => policy.All(entry => Enum.IsDefined(entry.Key) && Enum.IsDefined(entry.Value)))
            .WithMessage("Every policy entry must map a defined group role to a defined permission.");
}
