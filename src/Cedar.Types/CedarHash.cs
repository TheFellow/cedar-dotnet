using System;
using System.Buffers.Binary;
using System.Collections.Generic;
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

    public static int ForBytes(string discriminator, ReadOnlySpan<byte> value)
    {
        ulong hash = Start(discriminator);
        hash = Update(hash, value);
        return Finish(hash);
    }

    public static int ForStringPair(string discriminator, string first, string second)
    {
        ulong hash = Start(discriminator);
        hash = Update(hash, Encoding.UTF8.GetBytes(first));
        hash = Update(hash, (byte)0xff);
        hash = Update(hash, Encoding.UTF8.GetBytes(second));
        return Finish(hash);
    }

    public static int ForInt32Pair(string discriminator, int first, int second)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int) * 2];
        BinaryPrimitives.WriteInt32LittleEndian(bytes[..sizeof(int)], first);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[sizeof(int)..], second);

        ulong hash = Start(discriminator);
        hash = Update(hash, bytes);
        return Finish(hash);
    }

    public static int ForBytesAndInt32(string discriminator, ReadOnlySpan<byte> bytes, int value)
    {
        Span<byte> intBytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(intBytes, value);

        ulong hash = Start(discriminator);
        hash = Update(hash, bytes);
        hash = Update(hash, (byte)0xff);
        hash = Update(hash, intBytes);
        return Finish(hash);
    }

    public static int ForXorCollection(string discriminator, IEnumerable<int> itemHashes)
    {
        ulong combined = 0;
        int count = 0;

        foreach (int itemHash in itemHashes)
        {
            combined ^= unchecked((uint)itemHash);
            count++;
        }

        Span<byte> bytes = stackalloc byte[sizeof(ulong) + sizeof(int)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[..sizeof(ulong)], combined);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[sizeof(ulong)..], count);

        ulong hash = Start(discriminator);
        hash = Update(hash, bytes);
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
