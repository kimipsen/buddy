namespace buddy.Features.FlashCards;

// A guardian-typed card: text question and text answer only.
public sealed record FlashCard(FlashCardId Id, string Question, string Answer);
