using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Hkmp.Util;

/// <summary>
/// High-performance thread-safe cache with LRU eviction policy.
/// </summary>
internal sealed class FastCache<TKey, TValue> where TKey : notnull {
    private readonly ConcurrentDictionary<TKey, CacheEntry> _cache;
    private readonly int _maxSize;
    private readonly TimeSpan _expiration;

    private sealed class CacheEntry {
        public TValue Value;
        public DateTime LastAccess;
        
        public CacheEntry(TValue value) {
            Value = value;
            LastAccess = DateTime.UtcNow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateAccess() {
            LastAccess = DateTime.UtcNow;
        }
    }

    public FastCache(int maxSize = 1000, TimeSpan? expiration = null) {
        _maxSize = maxSize;
        _expiration = expiration ?? TimeSpan.FromMinutes(10);
        _cache = new ConcurrentDictionary<TKey, CacheEntry>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(TKey key, out TValue value) {
        if (_cache.TryGetValue(key, out var entry)) {
            if (DateTime.UtcNow - entry.LastAccess < _expiration) {
                entry.UpdateAccess();
                value = entry.Value;
                return true;
            }
            _cache.TryRemove(key, out _);
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory) {
        if (TryGet(key, out var value)) {
            return value;
        }

        var newValue = valueFactory(key);
        Add(key, newValue);
        return newValue;
    }

    public void Add(TKey key, TValue value) {
        if (_cache.Count >= _maxSize) {
            EvictOldest();
        }

        _cache[key] = new CacheEntry(value);
    }

    private void EvictOldest() {
        DateTime oldestTime = DateTime.MaxValue;
        TKey oldestKey = default;

        foreach (var kvp in _cache) {
            if (kvp.Value.LastAccess < oldestTime) {
                oldestTime = kvp.Value.LastAccess;
                oldestKey = kvp.Key;
            }
        }

        if (oldestKey != null) {
            _cache.TryRemove(oldestKey, out _);
        }
    }

    public void Clear() => _cache.Clear();
}

/// <summary>
/// Optimized struct-based cache for small value types to avoid boxing.
/// </summary>
internal sealed class ValueCache<TKey, TValue> 
    where TKey : notnull 
    where TValue : struct {
    
    private readonly struct Entry {
        public readonly TValue Value;
        public readonly long Timestamp;

        public Entry(TValue value, long timestamp) {
            Value = value;
            Timestamp = timestamp;
        }
    }

    private readonly ConcurrentDictionary<TKey, Entry> _cache;
    private readonly int _maxSize;
    private readonly long _expirationTicks;

    public ValueCache(int maxSize = 1000, TimeSpan? expiration = null) {
        _maxSize = maxSize;
        _expirationTicks = (expiration ?? TimeSpan.FromMinutes(5)).Ticks;
        _cache = new ConcurrentDictionary<TKey, Entry>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(TKey key, out TValue value) {
        if (_cache.TryGetValue(key, out var entry)) {
            if (DateTime.UtcNow.Ticks - entry.Timestamp < _expirationTicks) {
                value = entry.Value;
                return true;
            }
            _cache.TryRemove(key, out _);
        }

        value = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(TKey key, TValue value) {
        if (_cache.Count >= _maxSize) {
            var oldestKey = default(TKey);
            var oldestTime = long.MaxValue;

            foreach (var kvp in _cache) {
                if (kvp.Value.Timestamp < oldestTime) {
                    oldestTime = kvp.Value.Timestamp;
                    oldestKey = kvp.Key;
                }
            }

            if (oldestKey != null) {
                _cache.TryRemove(oldestKey, out _);
            }
        }

        _cache[key] = new Entry(value, DateTime.UtcNow.Ticks);
    }

    public void Clear() => _cache.Clear();
}
