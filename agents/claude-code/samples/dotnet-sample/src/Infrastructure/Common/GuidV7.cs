using System;

namespace Project.Infrastructure.Common;

internal static class GuidV7
{
    public static Guid NewGuid()
    {
        // UUIDv7: 48-bit unix timestamp in milliseconds + 80 bits of randomness
        var unixMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<byte> bytes = stackalloc byte[16];

        // 48-bit timestamp, big-endian
        bytes[0] = (byte)(unixMillis >> 40);
        bytes[1] = (byte)(unixMillis >> 32);
        bytes[2] = (byte)(unixMillis >> 24);
        bytes[3] = (byte)(unixMillis >> 16);
        bytes[4] = (byte)(unixMillis >> 8);
        bytes[5] = (byte)(unixMillis);

        // fill remaining bytes with cryptographic randomness
        var rand = System.Security.Cryptography.RandomNumberGenerator.Create();
        var tmp = new byte[10];
        rand.GetBytes(tmp);
        tmp.AsSpan().CopyTo(bytes.Slice(6));

        // set version to 7 (0b0111) in high 4 bits of byte 6
        bytes[6] = (byte)((bytes[6] & 0x0F) | (7 << 4));

        // set RFC 4122 variant (10xxxxxx) in byte 8
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }
}
