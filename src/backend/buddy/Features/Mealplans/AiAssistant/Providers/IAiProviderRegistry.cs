namespace buddy.Features.Mealplans;

public interface IAiProviderRegistry
{
    IAiChatClient Resolve(AiProvider provider);
}
