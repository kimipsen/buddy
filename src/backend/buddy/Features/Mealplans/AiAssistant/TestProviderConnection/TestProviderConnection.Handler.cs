using System.Text.Json;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class TestProviderConnectionHandler
{
    public static async Task<Result<TestProviderConnectionResult>> Handle(
        TestProviderConnection command,
        IValidator<TestProviderConnection> validator,
        IAiCredentialEventStore credentials,
        IGuardianLinkEventStore guardians,
        IApiKeyCipher cipher,
        IAiProviderRegistry providerRegistry,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<TestProviderConnectionResult>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<TestProviderConnectionResult>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<TestProviderConnectionResult>();
        }

        string apiKey;

        if (!string.IsNullOrEmpty(command.ApiKey))
        {
            apiKey = command.ApiKey;
        }
        else
        {
            var credentialId = await MealFamilyResolution.ResolveFamilyAiCredentialIdAsync(command.ChildId, guardians, credentials, cancellationToken);
            var credential = credentialId is null ? null : AiProviderCredential.Rehydrate(await credentials.ReadAsync(credentialId, cancellationToken));

            if (credential is null || !credential.Providers.TryGetValue(command.Provider, out var storedKey))
            {
                return new Result<TestProviderConnectionResult>.Validation(ValidationProblem.Of($"No API key has been stored for {command.Provider} yet."));
            }

            apiKey = cipher.Unprotect(storedKey.CipherText);
        }

        IAiChatClient client;

        try
        {
            client = providerRegistry.Resolve(command.Provider);
        }
        catch (NotSupportedException ex)
        {
            return new Result<TestProviderConnectionResult>.Validation(ValidationProblem.Of(ex.Message));
        }

        try
        {
            await client.SendAsync(
                new AiChatCompletionRequest(
                    apiKey,
                    "Reply with exactly the single word OK and nothing else.",
                    [new AiChatMessage(AiChatMessageRole.User, "Say OK.", [])],
                    []),
                cancellationToken);

            return new Result<TestProviderConnectionResult>.Success(new TestProviderConnectionResult(true, null));
        }
        catch (AiProviderException ex)
        {
            return new Result<TestProviderConnectionResult>.Success(new TestProviderConnectionResult(false, DescribeFailure(ex)));
        }
    }

    // Never surfaces the raw provider error body verbatim (it could be arbitrarily large or
    // formatted in a way that leaks more than intended) -- only a short, best-effort message.
    private static string DescribeFailure(AiProviderException ex)
    {
        try
        {
            using var document = JsonDocument.Parse(ex.ResponseBody);

            if (document.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? $"Request failed with status {(int)ex.StatusCode}.";
            }
        }
        catch (JsonException)
        {
            // Fall through to the generic message below.
        }

        return $"Request failed with status {(int)ex.StatusCode}.";
    }
}
