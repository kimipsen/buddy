namespace buddy.Features.Users;

internal enum CursorDirection : byte
{
    After = 0,
    Before = 1
}

internal readonly record struct DecodedCursor(long Version, CursorDirection Direction);

internal static class Cursor
{
    private const int EncodedLength = sizeof(long) + sizeof(byte);

    public static string EncodeAfter(long version) => Encode(version, CursorDirection.After);

    public static string EncodeBefore(long version) => Encode(version, CursorDirection.Before);

    public static bool TryDecode(string? cursor, out DecodedCursor decoded)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            decoded = new DecodedCursor(0, CursorDirection.After);
            return true;
        }

        Span<byte> bytes = stackalloc byte[EncodedLength];

        if (!Convert.TryFromBase64String(cursor, bytes, out var bytesWritten) || bytesWritten != EncodedLength)
        {
            decoded = default;
            return false;
        }

        var direction = (CursorDirection)bytes[0];

        if (direction != CursorDirection.After && direction != CursorDirection.Before)
        {
            decoded = default;
            return false;
        }

        var version = BitConverter.ToInt64(bytes[1..]);

        if (version < 0)
        {
            decoded = default;
            return false;
        }

        decoded = new DecodedCursor(version, direction);
        return true;
    }

    private static string Encode(long version, CursorDirection direction)
    {
        Span<byte> bytes = stackalloc byte[EncodedLength];
        bytes[0] = (byte)direction;
        BitConverter.TryWriteBytes(bytes[1..], version);

        return Convert.ToBase64String(bytes);
    }
}
