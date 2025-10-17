using System.Collections.Generic;
using Hkmp.Networking.Packet;

namespace Hkmp.Util;

/// <summary>
/// Centralized manager for all object pools in HKMP.
/// Reduces GC pressure through aggressive object reuse.
/// </summary>
internal static class PoolManager {
    private static readonly ObjectPool<List<byte>> ByteListPool;
    private static readonly ObjectPool<List<ushort>> UShortListPool;
    private static readonly ObjectPool<Dictionary<byte, object>> ByteDictionaryPool;
    private static readonly ObjectPool<HashSet<ushort>> UShortHashSetPool;
    private static readonly ObjectPool<PacketBuffer> PacketBufferPool;

    static PoolManager() {
        ByteListPool = new ObjectPool<List<byte>>(
            () => new List<byte>(256),
            list => list.Clear(),
            maxSize: 200
        );

        UShortListPool = new ObjectPool<List<ushort>>(
            () => new List<ushort>(64),
            list => list.Clear(),
            maxSize: 100
        );

        ByteDictionaryPool = new ObjectPool<Dictionary<byte, object>>(
            () => new Dictionary<byte, object>(16),
            dict => dict.Clear(),
            maxSize: 50
        );

        UShortHashSetPool = new ObjectPool<HashSet<ushort>>(
            () => new HashSet<ushort>(),
            set => set.Clear(),
            maxSize: 100
        );

        PacketBufferPool = new ObjectPool<PacketBuffer>(
            () => new PacketBuffer(),
            buffer => buffer.Reset(),
            maxSize: 150
        );
    }

    public static List<byte> RentByteList() => ByteListPool.Rent();
    public static void ReturnByteList(List<byte> list) => ByteListPool.Return(list);
    public static PooledObject<List<byte>> GetPooledByteList() => 
        new(ByteListPool, ByteListPool.Rent());

    public static List<ushort> RentUShortList() => UShortListPool.Rent();
    public static void ReturnUShortList(List<ushort> list) => UShortListPool.Return(list);
    public static PooledObject<List<ushort>> GetPooledUShortList() => 
        new(UShortListPool, UShortListPool.Rent());

    public static Dictionary<byte, object> RentByteDictionary() => ByteDictionaryPool.Rent();
    public static void ReturnByteDictionary(Dictionary<byte, object> dict) => ByteDictionaryPool.Return(dict);
    public static PooledObject<Dictionary<byte, object>> GetPooledByteDictionary() => 
        new(ByteDictionaryPool, ByteDictionaryPool.Rent());

    public static HashSet<ushort> RentUShortHashSet() => UShortHashSetPool.Rent();
    public static void ReturnUShortHashSet(HashSet<ushort> set) => UShortHashSetPool.Return(set);
    public static PooledObject<HashSet<ushort>> GetPooledUShortHashSet() => 
        new(UShortHashSetPool, UShortHashSetPool.Rent());

    public static PacketBuffer RentPacketBuffer() => PacketBufferPool.Rent();
    public static void ReturnPacketBuffer(PacketBuffer buffer) => PacketBufferPool.Return(buffer);
    public static PooledObject<PacketBuffer> GetPooledPacketBuffer() => 
        new(PacketBufferPool, PacketBufferPool.Rent());
}
