using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Hkmp.Eventing;

/// <summary>
/// High-performance event bus with minimal allocations and lock-free operations.
/// </summary>
internal sealed class FastEventBus {
    private static readonly Lazy<FastEventBus> _instance = new(() => new FastEventBus());
    public static FastEventBus Instance => _instance.Value;

    private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _subscriptions;

    private FastEventBus() {
        _subscriptions = new ConcurrentDictionary<Type, ConcurrentBag<Delegate>>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class {
        var eventType = typeof(TEvent);
        var handlers = _subscriptions.GetOrAdd(eventType, _ => new ConcurrentBag<Delegate>());
        handlers.Add(handler);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class {
        var eventType = typeof(TEvent);
        if (_subscriptions.TryGetValue(eventType, out var handlers)) {
            var newBag = new ConcurrentBag<Delegate>();
            foreach (var h in handlers) {
                if (!ReferenceEquals(h, handler)) {
                    newBag.Add(h);
                }
            }
            _subscriptions[eventType] = newBag;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish<TEvent>(TEvent eventData) where TEvent : class {
        var eventType = typeof(TEvent);
        if (!_subscriptions.TryGetValue(eventType, out var handlers)) return;

        foreach (var handler in handlers) {
            try {
                ((Action<TEvent>)handler)(eventData);
            } catch {
                // Swallow to prevent one handler from breaking others
            }
        }
    }

    public void Clear() => _subscriptions.Clear();
}

/// <summary>
/// Base event class for typed events.
/// </summary>
internal abstract class EventBase {
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Scoped event subscription that automatically unsubscribes on disposal.
/// </summary>
internal sealed class EventSubscription<TEvent> : IDisposable where TEvent : class {
    private readonly Action<TEvent> _handler;
    private bool _disposed;

    public EventSubscription(Action<TEvent> handler) {
        _handler = handler;
        FastEventBus.Instance.Subscribe(handler);
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        FastEventBus.Instance.Unsubscribe(_handler);
    }
}
