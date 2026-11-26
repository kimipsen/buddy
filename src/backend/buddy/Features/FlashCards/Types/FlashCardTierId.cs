namespace buddy.Features.FlashCards;

public sealed record FlashCardTierId(Guid Value)
{
    public static FlashCardTierId New() => new(Guid.CreateVersion7());
}
