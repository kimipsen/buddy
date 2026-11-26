namespace buddy.Features.FlashCards;

public sealed record FlashCardSetId(Guid Value)
{
    public static FlashCardSetId New() => new(Guid.CreateVersion7());
}
