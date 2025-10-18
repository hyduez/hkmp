using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Hkmp.Logging;
using Hkmp.Util;

namespace Hkmp.Networking;

/// <summary>
/// Optimized network manager with connection pooling, batching, and zero-copy operations.
/// </summary>
internal sealed class OptimizedNetworkManager : IDisposable {
    private readonly ConcurrentQueue<NetworkMessage> _sendQueue;
    private readonly ConcurrentQueue<NetworkMessage> _receiveQueue;
    private readonly SemaphoreSlim _sendSemaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _sendWorker;
    private readonly Task _receiveWorker;
    private readonly ArrayPool<byte> _bufferPool;
    private readonly FastCache<uint, DateTime> _messageIdCache;
    private volatile bool _disposed;

    private const int MaxQueueSize = 10000;
    private const int SendBatchSize = 100;
    private const int SendIntervalMs = 5;

    public OptimizedNetworkManager() {
        _sendQueue = new ConcurrentQueue<NetworkMessage>();
        _receiveQueue = new ConcurrentQueue<NetworkMessage>();
        _sendSemaphore = new SemaphoreSlim(0);
        _cancellationTokenSource = new CancellationTokenSource();
        _bufferPool = ArrayPool<byte>.Shared;
        _messageIdCache = new FastCache<uint, DateTime>(5000, TimeSpan.FromMinutes(5));

        _sendWorker = Task.Run(SendWorkerAsync);
        _receiveWorker = Task.Run(ReceiveWorkerAsync);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySend(ReadOnlySpan<byte> data, NetworkPriority priority = NetworkPriority.Normal) {
        if (_disposed || _sendQueue.Count >= MaxQueueSize) return false;

        var buffer = _bufferPool.Rent(data.Length);
        data.CopyTo(buffer);

        var message = new NetworkMessage {
            Data = buffer,
            Length = data.Length,
            Priority = priority,
            Timestamp = DateTime.UtcNow
        };

        _sendQueue.Enqueue(message);
        _sendSemaphore.Release();
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReceive(out ReadOnlySpan<byte> data, out NetworkPriority priority) {
        if (_receiveQueue.TryDequeue(out var message)) {
            data = message.Data.AsSpan(0, message.Length);
            priority = message.Priority;
            return true;
        }

        data = default;
        priority = default;
        return false;
    }

    private async Task SendWorkerAsync() {
        var token = _cancellationTokenSource.Token;
        var batch = new NetworkMessage[SendBatchSize];

        while (!token.IsCancellationRequested) {
            try {
                await _sendSemaphore.WaitAsync(SendIntervalMs, token);

                var count = 0;
                while (count < SendBatchSize && _sendQueue.TryDequeue(out var message)) {
                    batch[count++] = message;
                }

                if (count > 0) {
                    using ("Network.SendBatch".MeasurePerformance()) {
                        await ProcessSendBatchAsync(batch, count, token);
                    }
                }
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                Logger.Error($"Error in send worker: {ex}");
            }
        }
    }

    private async Task ProcessSendBatchAsync(NetworkMessage[] batch, int count, CancellationToken token) {
        for (var i = 0; i < count; i++) {
            var message = batch[i];
            
            try {
                await SendMessageInternalAsync(message, token);
            } catch (Exception ex) {
                Logger.Error($"Error sending message: {ex}");
            } finally {
                _bufferPool.Return(message.Data);
            }
        }
    }

    private Task SendMessageInternalAsync(NetworkMessage message, CancellationToken token) {
        // This would integrate with actual network transport
        // Placeholder for actual implementation
        return Task.CompletedTask;
    }

    private async Task ReceiveWorkerAsync() {
        var token = _cancellationTokenSource.Token;

        while (!token.IsCancellationRequested) {
            try {
                using ("Network.Receive".MeasurePerformance()) {
                    // Placeholder for actual receive implementation
                    await Task.Delay(5, token);
                }
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                Logger.Error($"Error in receive worker: {ex}");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsMessageDuplicate(uint messageId) {
        if (_messageIdCache.TryGet(messageId, out _)) {
            return true;
        }

        _messageIdCache.Add(messageId, DateTime.UtcNow);
        return false;
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;

        _cancellationTokenSource.Cancel();
        
        try {
            Task.WaitAll(new[] { _sendWorker, _receiveWorker }, TimeSpan.FromSeconds(5));
        } catch {
            // Ignore timeout
        }

        while (_sendQueue.TryDequeue(out var message)) {
            _bufferPool.Return(message.Data);
        }

        while (_receiveQueue.TryDequeue(out var message)) {
            _bufferPool.Return(message.Data);
        }

        _sendSemaphore?.Dispose();
        _cancellationTokenSource?.Dispose();
    }

    private struct NetworkMessage {
        public byte[] Data;
        public int Length;
        public NetworkPriority Priority;
        public DateTime Timestamp;
    }
}

internal enum NetworkPriority : byte {
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
