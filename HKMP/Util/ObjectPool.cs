using System;
using System.Collections.Concurrent;

namespace Hkmp.Util;

/// <summary>
/// High-performance object pool for reducing GC pressure.
/// </summary>
/// <typeparam name="T">The type of objects to pool.</typeparam>
internal class ObjectPool<T> where T : class {
    private readonly ConcurrentBag<T> _objects;
    private readonly Func<T> _objectGenerator;
    private readonly Action<T> _resetAction;
    private readonly int _maxSize;
    private int _count;

    public ObjectPool(Func<T> objectGenerator, Action<T> resetAction = null, int maxSize = 100) {
        _objects = new ConcurrentBag<T>();
        _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
        _resetAction = resetAction;
        _maxSize = maxSize;
    }

    public T Rent() {
        return _objects.TryTake(out var item) ? item : _objectGenerator();
    }

    public void Return(T item) {
        if (item == null || _count >= _maxSize) return;
        
        _resetAction?.Invoke(item);
        _objects.Add(item);
        _count++;
    }
}

/// <summary>
/// Provides pooled objects that automatically return to pool when disposed.
/// </summary>
internal readonly struct PooledObject<T> : IDisposable where T : class {
    private readonly ObjectPool<T> _pool;
    public readonly T Object;

    public PooledObject(ObjectPool<T> pool, T obj) {
        _pool = pool;
        Object = obj;
    }

    public void Dispose() {
        _pool.Return(Object);
    }
}
