namespace buddy.Features.Mealplans;

public sealed class AiProviderRegistry(AnthropicChatClient anthropic, OpenAiChatClient openAi, GeminiChatClient gemini) : IAiProviderRegistry
{
    public IAiChatClient Resolve(AiProvider provider) => provider switch
    {
        AiProvider.Anthropic => anthropic,
        AiProvider.OpenAi => openAi,
        AiProvider.Gemini => gemini,
        _ => throw new NotSupportedException($"Unrecognized AI provider: {provider}."),
    };
}
