namespace buddy.Features.FlashCards;

// Structural limits shared by the validators and the handlers.
public static class FlashCardLimits
{
    public const int NameMaxLength = 100;
    public const int TextMaxLength = 200;
    public const int MaxCardsPerSet = 200;

    public const int MinTiers = 1;
    public const int MaxTiers = 5;
    public const int MinTierCards = 3;
    public const int MaxTierCards = 50;
    public const int MinTierStars = 1;
    public const int MaxTierStars = 20;
}
