using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hkmp.Util;

/// <summary>
/// High-performance batch processor for aggregating operations and reducing overhead.
/// </summary>
internal sealed class BatchProcessor<T> : IDisposable {
    private readonly ConcurrentQueue<T> _queue;
    private readonly Action<List<T>> _batchHandler;
    private readonly int _batchSize;
    private readonly int _flushIntervalMs;
    private readonly Timer _flushTimer;
    private readonly List<T> _processingBuffer;
    private volatile bool _disposed;

    public BatchProcessor(
        Action<List<T>> batchHandler,
        int batchSize = 50,
        int flushIntervalMs = 100) {
        
        _queue = new ConcurrentQueue<T>();
        _batchHandler = batchHandler ?? throw new ArgumentNullException(nameof(batchHandler));
        _batchSize = batchSize;
        _flushIntervalMs = flushIntervalMs;
        _processingBuffer = new List<T>(batchSize);

        _flushTimer = new Timer(_ => FlushIfNeeded(), null, flushIntervalMs, flushIntervalMs);
    }

    public void Enqueue(T item) {
        if (_disposed) return;

        _queue.Enqueue(item);

        if (_queue.Count >= _batchSize) {
            Task.Run(() => Flush());
        }
    }

    public void EnqueueRange(IEnumerable<T> items) {
        if (_disposed) return;

        foreach (var item in items) {
            _queue.Enqueue(item);
        }

        if (_queue.Count >= _batchSize) {
            Task.Run(() => Flush());
        }
    }

    private void FlushIfNeeded() {
        if (!_disposed && _queue.Count > 0) {
            Flush();
        }
    }

    public void Flush() {
        if (_disposed) return;

        _processingBuffer.Clear();

        var itemsToProcess = System.Math.Min(_queue.Count, _batchSize);
        for (var i = 0; i < itemsToProcess; i++) {
            if (_queue.TryDequeue(out var item)) {
                _processingBuffer.Add(item);
            }
        }

        if (_processingBuffer.Count > 0) {
            try {
                _batchHandler(_processingBuffer);
            } catch {
                // Swallow exceptions to prevent crashes
            }
        }
    }

    public void Dispose() {
        if (_disposed) return;
        
        _disposed = true;
        _flushTimer?.Dispose();
        Flush(); // Final flush
    }
}

/// <summary>
/// Specialized batch processor with priority support.
/// </summary>
internal sealed class PriorityBatchProcessor<T> : IDisposable {
    private readonly ConcurrentQueue<(T Item, int Priority)> _queue;
    private readonly Action<List<T>> _batchHandler;
    private readonly int _batchSize;
    private readonly int _flushIntervalMs;
    private readonly Timer _flushTimer;
    private volatile bool _disposed;

    public PriorityBatchProcessor(
        Action<List<T>> batchHandler,
        int batchSize = 50,
        int flushIntervalMs = 100) {
        
        _queue = new ConcurrentQueue<(T, int)>();
        _batchHandler = batchHandler ?? throw new ArgumentNullException(nameof(batchHandler));
        _batchSize = batchSize;
        _flushIntervalMs = flushIntervalMs;

        _flushTimer = new Timer(_ => FlushIfNeeded(), null, flushIntervalMs, flushIntervalMs);
    }

    public void Enqueue(T item, int priority = 0) {
        if (_disposed) return;

        _queue.Enqueue((item, priority));

        if (_queue.Count >= _batchSize) {
            Task.Run(() => Flush());
        }
    }

    private void FlushIfNeeded() {
        if (!_disposed && _queue.Count > 0) {
            Flush();
        }
    }

    public void Flush() {
        if (_disposed) return;

        var items = new List<(T Item, int Priority)>();
        var itemsToProcess = System.Math.Min(_queue.Count, _batchSize);

        for (var i = 0; i < itemsToProcess; i++) {
            if (_queue.TryDequeue(out var item)) {
                items.Add(item);
            }
        }

        if (items.Count == 0) return;

        items.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        var sortedItems = new List<T>(items.Count);
        foreach (var (item, _) in items) {
            sortedItems.Add(item);
        }

        try {
            _batchHandler(sortedItems);
        } catch {
            // Swallow exceptions to prevent crashes
        }
    }

    public void Dispose() {
        if (_disposed) return;
        
        _disposed = true;
        _flushTimer?.Dispose();
        Flush();
    }
}
