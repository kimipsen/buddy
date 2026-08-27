using FluentValidation;

namespace buddy.Common.Validation;

public static class ValidatorExtensions
{
    // Mirrors the early-return shape every handler already uses for its own checks (e.g.
    // AssignPickupHandler.ValidateFields): null means valid, a non-null result is returned
    // straight from the caller as a Result<T>.Validation/outcome-specific Validation case.
    public static async Task<ValidationProblem?> ValidateCommandAsync<T>(
        this IValidator<T> validator, T command, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(command, cancellationToken);

        return result.IsValid ? null : ValidationProblem.FromFluentValidation(result);
    }
}
