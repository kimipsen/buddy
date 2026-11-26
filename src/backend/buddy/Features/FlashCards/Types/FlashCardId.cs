namespace buddy.Features.FlashCards;

public sealed record FlashCardId(Guid Value)
{
    public static FlashCardId New() => new(Guid.CreateVersion7());
}
