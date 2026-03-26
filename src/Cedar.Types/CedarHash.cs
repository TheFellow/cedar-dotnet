using System;
using System.Buffers.Binary;
using System.Text;

namespace Cedar.Types;

internal static class CedarHash
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static int ForBoolean(string discriminator, bool value)
    {
        ulong hash = Start(discriminator);
        hash = Update(hash, value ? (byte)1 : (byte)0);
        return Finish(hash);
    }

    public static int ForInt64(string discriminator, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);

        ulong hash = Start(discriminator);
        hash = Update(hash, bytes);
        return Finish(hash);
    }

    public static int ForString(string discriminator, string value)
    {
        ulong hash = Start(discriminator);
        hash = Update(hash, Encoding.UTF8.GetBytes(value));
        return Finish(hash);
    }

    private static ulong Start(string discriminator)
    {
        ulong hash = OffsetBasis;
        hash = Update(hash, Encoding.UTF8.GetBytes(discriminator));
        return Update(hash, (byte)0xff);
    }

    private static ulong Update(ulong hash, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            hash ^= value;
            hash *= Prime;
        }

        return hash;
    }

    private static ulong Update(ulong hash, byte value)
    {
        hash ^= value;
        hash *= Prime;
        return hash;
    }

    private static int Finish(ulong hash)
    {
        return unchecked((int)(hash ^ (hash >> 32)));
    }
}
