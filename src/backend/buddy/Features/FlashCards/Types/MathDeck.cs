using System.Globalization;

namespace buddy.Features.FlashCards;

// One generated card. Left/Operation/Right are exposed separately so the UI never parses Question.
public sealed record MathCard(string Key, string Question, string Answer, int Left, MathOperation Operation, int Right);

// Generates the cards of a built-in math set. Cards are never stored: a set holds only its MathRule,
// and every card is derived from it, with a deterministic CardKey ("add:4+5", "sub:9-4", "mul:7x3",
// "div:21/7") that maps a stored result back to the same card in any later game.
//
// Subtraction cannot go negative and division cannot be fractional by construction: the generators
// below only ever emit those cards.
public static class MathDeck
{
    public const int TableSize = 10;

    public static bool IsValidLimit(MathOperation operation, int limit) => operation switch
    {
        MathOperation.Addition or MathOperation.Subtraction => limit is >= 2 and <= 100,
        MathOperation.Multiplication or MathOperation.Division => limit is >= 1 and <= 12,
        _ => false
    };

    public static IEnumerable<MathCard> Cards(MathRule rule)
    {
        var limit = rule.Limit;

        switch (rule.Operation)
        {
            case MathOperation.Addition:
                for (var sum = 0; sum <= limit; sum++)
                {
                    for (var left = 0; left <= sum; left++)
                    {
                        yield return Make(MathOperation.Addition, left, sum - left);
                    }
                }

                break;
            case MathOperation.Subtraction:
                for (var minuend = 0; minuend <= limit; minuend++)
                {
                    for (var subtrahend = 0; subtrahend <= minuend; subtrahend++)
                    {
                        yield return Make(MathOperation.Subtraction, minuend, subtrahend);
                    }
                }

                break;
            case MathOperation.Multiplication:
                for (var k = 1; k <= TableSize; k++)
                {
                    yield return Make(MathOperation.Multiplication, limit, k);
                }

                break;
            case MathOperation.Division:
                for (var k = 1; k <= TableSize; k++)
                {
                    yield return Make(MathOperation.Division, limit * k, limit);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(rule), rule.Operation, "Unknown math operation.");
        }
    }

    // Closed form, so list responses and finish validation never rebuild a whole deck: a test pins
    // it against Cards(rule).Count() over every allowed limit.
    public static int UniqueCount(MathRule rule) => rule.Operation switch
    {
        MathOperation.Addition or MathOperation.Subtraction => (rule.Limit + 1) * (rule.Limit + 2) / 2,
        MathOperation.Multiplication or MathOperation.Division => TableSize,
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Operation, "Unknown math operation.")
    };

    // Parses a CardKey and checks it belongs to the rule's deck. Membership is a parse, not a deck
    // rebuild. Only the canonical spelling is accepted (no leading zeros or signs), so one card has
    // exactly one key.
    public static bool TryGetCard(MathRule rule, string? key, out MathCard card)
    {
        card = null!;

        if (key is null || key.Length < 5 || key[3] != ':')
        {
            return false;
        }

        var prefix = key[..3];
        var body = key[4..];

        var (operation, separator) = prefix switch
        {
            "add" => (MathOperation.Addition, '+'),
            "sub" => (MathOperation.Subtraction, '-'),
            "mul" => (MathOperation.Multiplication, 'x'),
            "div" => (MathOperation.Division, '/'),
            _ => (default(MathOperation), '\0')
        };

        if (separator == '\0' || operation != rule.Operation)
        {
            return false;
        }

        var parts = body.Split(separator);

        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var left)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var right))
        {
            return false;
        }

        var member = operation switch
        {
            MathOperation.Addition => left + right <= rule.Limit,
            MathOperation.Subtraction => right <= left && left <= rule.Limit,
            MathOperation.Multiplication => left == rule.Limit && right is >= 1 and <= TableSize,
            MathOperation.Division => right == rule.Limit && left % rule.Limit == 0 && left / rule.Limit is >= 1 and <= TableSize,
            _ => false
        };

        if (!member)
        {
            return false;
        }

        var candidate = Make(operation, left, right);

        if (candidate.Key != key)
        {
            return false;
        }

        card = candidate;
        return true;
    }

    // A game's deck: exactly count cards drawn from the rule's unique cards (see CardDeck).
    public static IReadOnlyList<MathCard> Draw(MathRule rule, int count, Random random) =>
        CardDeck.Draw([.. Cards(rule)], count, random);

    private static MathCard Make(MathOperation operation, int left, int right)
    {
        var (prefix, symbol, answer) = operation switch
        {
            MathOperation.Addition => ("add", "+", left + right),
            MathOperation.Subtraction => ("sub", "-", left - right),
            MathOperation.Multiplication => ("mul", "x", left * right),
            MathOperation.Division => ("div", "/", left / right),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown math operation.")
        };

        var l = left.ToString(CultureInfo.InvariantCulture);
        var r = right.ToString(CultureInfo.InvariantCulture);

        return new MathCard(
            $"{prefix}:{l}{symbol}{r}",
            $"{l} {symbol} {r}",
            answer.ToString(CultureInfo.InvariantCulture),
            left,
            operation,
            right);
    }
}
