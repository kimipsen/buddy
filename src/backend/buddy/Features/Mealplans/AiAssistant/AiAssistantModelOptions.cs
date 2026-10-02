using System.ComponentModel.DataAnnotations;

namespace buddy.Features.Mealplans;

// Defaults are known-good models as of this feature's authoring, not necessarily each provider's
// current flagship -- both providers ship new models faster than this file gets touched, so these
// are meant to be overridden via config as needed, until the family-facing model picker (see the AI
// mealplan plan's build order) makes this a per-family choice instead of one app-wide default.
public sealed class AiAssistantModelOptions
{
    public const string SectionName = "AiAssistant";

    [Required]
    public string OpenAiModel { get; init; } = "gpt-4.1";

    [Required]
    public string GeminiModel { get; init; } = "gemini-2.5-flash";

    [Required]
    public string AnthropicModel { get; init; } = "claude-sonnet-5";
}
