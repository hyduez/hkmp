using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hkmp.Math;

namespace Hkmp.Serialization;

/// <summary>
/// Ultra-fast serialization using unsafe code and minimal allocations.
/// </summary>
internal static class FastSerializer {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteVector2(Span<byte> buffer, ref int offset, in Vector2 value) {
        fixed (byte* ptr = &buffer[offset]) {
            *(Vector2*)ptr = value;
        }
        offset += sizeof(float) * 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector2 ReadVector2(ReadOnlySpan<byte> buffer, ref int offset) {
        fixed (byte* ptr = &buffer[offset]) {
            var result = *(Vector2*)ptr;
            offset += sizeof(float) * 2;
            return result;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteVector3(Span<byte> buffer, ref int offset, in Vector3 value) {
        fixed (byte* ptr = &buffer[offset]) {
            *(Vector3*)ptr = value;
        }
        offset += sizeof(float) * 3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector3 ReadVector3(ReadOnlySpan<byte> buffer, ref int offset) {
        fixed (byte* ptr = &buffer[offset]) {
            var result = *(Vector3*)ptr;
            offset += sizeof(float) * 3;
            return result;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteCompressedFloat(Span<byte> buffer, ref int offset, float value, float min, float max, int bits = 16) {
        var range = max - min;
        var normalizedValue = Mathf.Clamp01((value - min) / range);
        var maxIntValue = (1 << bits) - 1;
        var intValue = (ushort)(normalizedValue * maxIntValue);

        buffer[offset++] = (byte)(intValue & 0xFF);
        buffer[offset++] = (byte)((intValue >> 8) & 0xFF);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadCompressedFloat(ReadOnlySpan<byte> buffer, ref int offset, float min, float max, int bits = 16) {
        var intValue = buffer[offset++] | (buffer[offset++] << 8);
        var maxIntValue = (1 << bits) - 1;
        var normalizedValue = intValue / (float)maxIntValue;
        return min + normalizedValue * (max - min);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteCompressedVector2(Span<byte> buffer, ref int offset, in Vector2 value, float minX, float maxX, float minY, float maxY) {
        WriteCompressedFloat(buffer, ref offset, value.X, minX, maxX);
        WriteCompressedFloat(buffer, ref offset, value.Y, minY, maxY);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ReadCompressedVector2(ReadOnlySpan<byte> buffer, ref int offset, float minX, float maxX, float minY, float maxY) {
        var x = ReadCompressedFloat(buffer, ref offset, minX, maxX);
        var y = ReadCompressedFloat(buffer, ref offset, minY, maxY);
        return new Vector2(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalculateVarintSize(ulong value) {
        var size = 1;
        while (value >= 0x80) {
            value >>= 7;
            size++;
        }
        return size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteVarint(Span<byte> buffer, ref int offset, ulong value) {
        while (value >= 0x80) {
            buffer[offset++] = (byte)(value | 0x80);
            value >>= 7;
        }
        buffer[offset++] = (byte)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReadVarint(ReadOnlySpan<byte> buffer, ref int offset) {
        ulong result = 0;
        var shift = 0;

        while (true) {
            var b = buffer[offset++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }

        return result;
    }
}

/// <summary>
/// Pooled serialization context for zero-allocation serialization.
/// </summary>
internal sealed class SerializationContext : IDisposable {
    private byte[] _buffer;
    private int _position;
    private readonly ArrayPool<byte> _pool;

    public SerializationContext(int initialSize = 1024) {
        _pool = ArrayPool<byte>.Shared;
        _buffer = _pool.Rent(initialSize);
        _position = 0;
    }

    public Span<byte> Buffer => _buffer.AsSpan(0, _position);
    public int Position => _position;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int required) {
        if (_position + required <= _buffer.Length) return;

        var newSize = System.Math.Max(_buffer.Length * 2, _position + required);
        var newBuffer = _pool.Rent(newSize);
        _buffer.AsSpan(0, _position).CopyTo(newBuffer);
        _pool.Return(_buffer);
        _buffer = newBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(byte value) {
        EnsureCapacity(1);
        _buffer[_position++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBytes(ReadOnlySpan<byte> data) {
        EnsureCapacity(data.Length);
        data.CopyTo(_buffer.AsSpan(_position));
        _position += data.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVector2(in Vector2 value) {
        EnsureCapacity(sizeof(float) * 2);
        FastSerializer.WriteVector2(_buffer, ref _position, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVector3(in Vector3 value) {
        EnsureCapacity(sizeof(float) * 3);
        FastSerializer.WriteVector3(_buffer, ref _position, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteVarint(ulong value) {
        var size = FastSerializer.CalculateVarintSize(value);
        EnsureCapacity(size);
        FastSerializer.WriteVarint(_buffer, ref _position, value);
    }

    public void Reset() {
        _position = 0;
    }

    public void Dispose() {
        if (_buffer != null) {
            _pool.Return(_buffer);
            _buffer = null;
        }
    }
}

/// <summary>
/// Math utilities for serialization.
/// </summary>
internal static class Mathf {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp01(float value) {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Clamp(float value, float min, float max) {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
