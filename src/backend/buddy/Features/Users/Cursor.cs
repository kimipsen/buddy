namespace buddy.Features.Users;

internal static class Cursor
{
    public static string Encode(long position) => Convert.ToBase64String(BitConverter.GetBytes(position));

    public static bool TryDecode(string? cursor, out long position)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            position = 0;
            return true;
        }

        Span<byte> bytes = stackalloc byte[sizeof(long)];

        if (!Convert.TryFromBase64String(cursor, bytes, out var bytesWritten) || bytesWritten != sizeof(long))
        {
            position = 0;
            return false;
        }

        position = BitConverter.ToInt64(bytes);
        return position >= 0;
    }
}
