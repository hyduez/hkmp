using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Hkmp.Math;

namespace Hkmp.Networking.Packet;

/// <summary>
/// High-performance packet buffer using ArrayPool to reduce allocations.
/// </summary>
internal sealed class PacketBuffer : IDisposable {
    private byte[] _buffer;
    private int _writePosition;
    private int _readPosition;
    private readonly ArrayPool<byte> _arrayPool;
    private const int InitialSize = 1024;
    private const int MaxSize = 65536;

    public int Length => _writePosition;
    public int RemainingReadBytes => _writePosition - _readPosition;

    public PacketBuffer(int initialCapacity = InitialSize) {
        _arrayPool = ArrayPool<byte>.Shared;
        _buffer = _arrayPool.Rent(initialCapacity);
        _writePosition = 0;
        _readPosition = 0;
    }

    public PacketBuffer(byte[] data) : this(data.Length) {
        Write(data);
        _readPosition = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int required) {
        if (_writePosition + required <= _buffer.Length) return;

        var newSize = System.Math.Min(_buffer.Length * 2, MaxSize);
        while (newSize < _writePosition + required && newSize < MaxSize) {
            newSize *= 2;
        }

        var newBuffer = _arrayPool.Rent(newSize);
        Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _writePosition);
        _arrayPool.Return(_buffer);
        _buffer = newBuffer;
    }

    public void Reset() {
        _writePosition = 0;
        _readPosition = 0;
    }

    public Span<byte> AsSpan() => _buffer.AsSpan(0, _writePosition);
    public ReadOnlySpan<byte> AsReadOnlySpan() => _buffer.AsSpan(0, _writePosition);

    public byte[] ToArray() {
        var result = new byte[_writePosition];
        Buffer.BlockCopy(_buffer, 0, result, 0, _writePosition);
        return result;
    }

    // Write operations using Span<T> for performance
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte value) {
        EnsureCapacity(1);
        _buffer[_writePosition++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ReadOnlySpan<byte> data) {
        EnsureCapacity(data.Length);
        data.CopyTo(_buffer.AsSpan(_writePosition));
        _writePosition += data.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort value) {
        EnsureCapacity(sizeof(ushort));
        BitConverter.TryWriteBytes(_buffer.AsSpan(_writePosition), value);
        _writePosition += sizeof(ushort);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(uint value) {
        EnsureCapacity(sizeof(uint));
        BitConverter.TryWriteBytes(_buffer.AsSpan(_writePosition), value);
        _writePosition += sizeof(uint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ulong value) {
        EnsureCapacity(sizeof(ulong));
        BitConverter.TryWriteBytes(_buffer.AsSpan(_writePosition), value);
        _writePosition += sizeof(ulong);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(short value) {
        EnsureCapacity(sizeof(short));
        BitConverter.TryWriteBytes(_buffer.AsSpan(_writePosition), value);
        _writePosition += sizeof(short);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(int value) {
        EnsureCapacity(sizeof(int));
        BitConverter.TryWriteBytes(_buffer.AsSpan(_writePosition), value);
        _writePosition += sizeof(int);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(long value) {
        EnsureCapacity(sizeof(long));
        BitConverter.TryWriteBytes(_buffer.AsSpan(_writePosition), value);
        _writePosition += sizeof(long);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(float value) {
        EnsureCapacity(sizeof(float));
        BitConverter.TryWriteBytes(_buffer.AsSpan(_writePosition), value);
        _writePosition += sizeof(float);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(double value) {
        EnsureCapacity(sizeof(double));
        BitConverter.TryWriteBytes(_buffer.AsSpan(_writePosition), value);
        _writePosition += sizeof(double);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(bool value) {
        Write((byte)(value ? 1 : 0));
    }

    public void Write(string value) {
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
        EnsureCapacity(maxByteCount + sizeof(ushort));

        var bytesWritten = Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _writePosition + sizeof(ushort));
        
        if (bytesWritten > ushort.MaxValue) {
            throw new ArgumentException($"String too long: {bytesWritten} bytes");
        }

        BitConverter.TryWriteBytes(_buffer.AsSpan(_writePosition), (ushort)bytesWritten);
        _writePosition += sizeof(ushort) + bytesWritten;
    }

    public void Write(Vector2 value) {
        Write(value.X);
        Write(value.Y);
    }

    public void Write(Vector3 value) {
        Write(value.X);
        Write(value.Y);
        Write(value.Z);
    }

    // Read operations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCanRead(int bytes) {
        if (_readPosition + bytes > _writePosition) {
            throw new InvalidOperationException($"Cannot read {bytes} bytes from buffer");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte() {
        EnsureCanRead(1);
        return _buffer[_readPosition++];
    }

    public ReadOnlySpan<byte> ReadBytes(int count) {
        EnsureCanRead(count);
        var span = _buffer.AsSpan(_readPosition, count);
        _readPosition += count;
        return span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUShort() {
        EnsureCanRead(sizeof(ushort));
        var value = BitConverter.ToUInt16(_buffer, _readPosition);
        _readPosition += sizeof(ushort);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt() {
        EnsureCanRead(sizeof(uint));
        var value = BitConverter.ToUInt32(_buffer, _readPosition);
        _readPosition += sizeof(uint);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadULong() {
        EnsureCanRead(sizeof(ulong));
        var value = BitConverter.ToUInt64(_buffer, _readPosition);
        _readPosition += sizeof(ulong);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadShort() {
        EnsureCanRead(sizeof(short));
        var value = BitConverter.ToInt16(_buffer, _readPosition);
        _readPosition += sizeof(short);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt() {
        EnsureCanRead(sizeof(int));
        var value = BitConverter.ToInt32(_buffer, _readPosition);
        _readPosition += sizeof(int);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadLong() {
        EnsureCanRead(sizeof(long));
        var value = BitConverter.ToInt64(_buffer, _readPosition);
        _readPosition += sizeof(long);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat() {
        EnsureCanRead(sizeof(float));
        var value = BitConverter.ToSingle(_buffer, _readPosition);
        _readPosition += sizeof(float);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble() {
        EnsureCanRead(sizeof(double));
        var value = BitConverter.ToDouble(_buffer, _readPosition);
        _readPosition += sizeof(double);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool() {
        return ReadByte() != 0;
    }

    public string ReadString() {
        var length = ReadUShort();
        if (length == 0) return string.Empty;

        EnsureCanRead(length);
        var value = Encoding.UTF8.GetString(_buffer, _readPosition, length);
        _readPosition += length;
        return value;
    }

    public Vector2 ReadVector2() => new(ReadFloat(), ReadFloat());
    public Vector3 ReadVector3() => new(ReadFloat(), ReadFloat(), ReadFloat());

    public void Dispose() {
        if (_buffer != null) {
            _arrayPool.Return(_buffer);
            _buffer = null;
        }
    }
}
