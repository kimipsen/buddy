namespace buddy.Features.FlashCards;

public enum MathOperation
{
    Addition,
    Subtraction,
    Multiplication,
    Division
}

// What Limit means depends on the operation (see MathDeck): the largest sum or minuend for
// addition and subtraction, the table for multiplication, the divisor for division.
public sealed record MathRule(MathOperation Operation, int Limit);
