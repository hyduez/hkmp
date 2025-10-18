using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Hkmp.Logging;

namespace Hkmp.Networking;

/// <summary>
/// High-performance connection pool with health checking and automatic recovery.
/// </summary>
internal sealed class ConnectionPool<TConnection> : IDisposable where TConnection : class {
    private readonly ConcurrentBag<PooledConnection> _availableConnections;
    private readonly ConcurrentDictionary<int, PooledConnection> _activeConnections;
    private readonly Func<Task<TConnection>> _connectionFactory;
    private readonly Action<TConnection> _connectionReset;
    private readonly Func<TConnection, bool> _connectionValidator;
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly TimeSpan _connectionTimeout;
    private readonly TimeSpan _healthCheckInterval;
    private readonly Timer _healthCheckTimer;
    private readonly SemaphoreSlim _connectionSemaphore;
    private volatile bool _disposed;
    private int _currentSize;

    private sealed class PooledConnection {
        public TConnection Connection { get; set; }
        public DateTime CreatedAt { get; }
        public DateTime LastUsed { get; set; }
        public int UseCount { get; set; }
        public bool IsHealthy { get; set; }
        public int Id { get; }

        private static int _nextId;

        public PooledConnection(TConnection connection) {
            Connection = connection;
            CreatedAt = DateTime.UtcNow;
            LastUsed = DateTime.UtcNow;
            UseCount = 0;
            IsHealthy = true;
            Id = Interlocked.Increment(ref _nextId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkUsed() {
            LastUsed = DateTime.UtcNow;
            UseCount++;
        }
    }

    public ConnectionPool(
        Func<Task<TConnection>> connectionFactory,
        Action<TConnection> connectionReset = null,
        Func<TConnection, bool> connectionValidator = null,
        int minSize = 2,
        int maxSize = 20,
        TimeSpan? connectionTimeout = null,
        TimeSpan? healthCheckInterval = null) {

        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _connectionReset = connectionReset;
        _connectionValidator = connectionValidator;
        _minSize = minSize;
        _maxSize = maxSize;
        _connectionTimeout = connectionTimeout ?? TimeSpan.FromMinutes(30);
        _healthCheckInterval = healthCheckInterval ?? TimeSpan.FromSeconds(30);

        _availableConnections = new ConcurrentBag<PooledConnection>();
        _activeConnections = new ConcurrentDictionary<int, PooledConnection>();
        _connectionSemaphore = new SemaphoreSlim(maxSize, maxSize);

        _healthCheckTimer = new Timer(_ => Task.Run(PerformHealthCheckAsync), null, _healthCheckInterval, _healthCheckInterval);

        Task.Run(InitializePoolAsync);
    }

    private async Task InitializePoolAsync() {
        try {
            for (var i = 0; i < _minSize; i++) {
                var connection = await CreateConnectionAsync();
                if (connection != null) {
                    _availableConnections.Add(connection);
                }
            }

            Logger.Info($"Connection pool initialized with {_currentSize} connections");
        } catch (Exception ex) {
            Logger.Error($"Error initializing connection pool: {ex}");
        }
    }

    private async Task<PooledConnection> CreateConnectionAsync() {
        if (_currentSize >= _maxSize) return null;

        try {
            var connection = await _connectionFactory();
            if (connection == null) return null;

            Interlocked.Increment(ref _currentSize);
            return new PooledConnection(connection);
        } catch (Exception ex) {
            Logger.Error($"Error creating connection: {ex}");
            return null;
        }
    }

    public async Task<TConnection> AcquireAsync(TimeSpan? timeout = null) {
        if (_disposed) throw new ObjectDisposedException(nameof(ConnectionPool<TConnection>));

        var timeoutValue = timeout ?? TimeSpan.FromSeconds(10);
        if (!await _connectionSemaphore.WaitAsync(timeoutValue)) {
            throw new TimeoutException("Failed to acquire connection from pool");
        }

        while (true) {
            if (_availableConnections.TryTake(out var pooled)) {
                if (IsConnectionValid(pooled)) {
                    pooled.MarkUsed();
                    _activeConnections[pooled.Id] = pooled;
                    _connectionReset?.Invoke(pooled.Connection);
                    return pooled.Connection;
                }

                await DisposeConnectionAsync(pooled);
            } else {
                var newConnection = await CreateConnectionAsync();
                if (newConnection != null) {
                    newConnection.MarkUsed();
                    _activeConnections[newConnection.Id] = newConnection;
                    return newConnection.Connection;
                }
            }

            if (_currentSize >= _maxSize) {
                await Task.Delay(50);
            }
        }
    }

    public void Release(TConnection connection) {
        if (_disposed || connection == null) return;

        var pooled = _activeConnections.Values.FirstOrDefault(p => ReferenceEquals(p.Connection, connection));
        if (pooled == null) return;

        if (_activeConnections.TryRemove(pooled.Id, out _)) {
            if (IsConnectionValid(pooled)) {
                _availableConnections.Add(pooled);
            } else {
                Task.Run(() => DisposeConnectionAsync(pooled));
            }

            _connectionSemaphore.Release();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsConnectionValid(PooledConnection pooled) {
        if (!pooled.IsHealthy) return false;

        var age = DateTime.UtcNow - pooled.CreatedAt;
        if (age > _connectionTimeout) return false;

        if (_connectionValidator != null && !_connectionValidator(pooled.Connection)) {
            pooled.IsHealthy = false;
            return false;
        }

        return true;
    }

    private async Task PerformHealthCheckAsync() {
        if (_disposed) return;

        var unhealthyConnections = new List<PooledConnection>();

        foreach (var pooled in _availableConnections) {
            if (!IsConnectionValid(pooled)) {
                unhealthyConnections.Add(pooled);
            }
        }

        foreach (var pooled in unhealthyConnections) {
            await DisposeConnectionAsync(pooled);
        }

        var currentAvailable = _availableConnections.Count;
        if (currentAvailable < _minSize) {
            var toCreate = _minSize - currentAvailable;
            for (var i = 0; i < toCreate && _currentSize < _maxSize; i++) {
                var connection = await CreateConnectionAsync();
                if (connection != null) {
                    _availableConnections.Add(connection);
                }
            }
        }
    }

    private async Task DisposeConnectionAsync(PooledConnection pooled) {
        try {
            if (pooled.Connection is IDisposable disposable) {
                disposable.Dispose();
            }

            if (pooled.Connection is IAsyncDisposable asyncDisposable) {
                await asyncDisposable.DisposeAsync();
            }

            Interlocked.Decrement(ref _currentSize);
        } catch (Exception ex) {
            Logger.Error($"Error disposing connection: {ex}");
        }
    }

    public (int total, int available, int active) GetStats() {
        return (_currentSize, _availableConnections.Count, _activeConnections.Count);
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;

        _healthCheckTimer?.Dispose();

        foreach (var pooled in _availableConnections) {
            DisposeConnectionAsync(pooled).GetAwaiter().GetResult();
        }

        foreach (var pooled in _activeConnections.Values) {
            DisposeConnectionAsync(pooled).GetAwaiter().GetResult();
        }

        _availableConnections.Clear();
        _activeConnections.Clear();
        _connectionSemaphore?.Dispose();
    }
}

/// <summary>
/// Scoped connection that automatically returns to pool on disposal.
/// </summary>
internal readonly struct PooledConnectionScope<TConnection> : IDisposable where TConnection : class {
    private readonly ConnectionPool<TConnection> _pool;
    public readonly TConnection Connection;

    public PooledConnectionScope(ConnectionPool<TConnection> pool, TConnection connection) {
        _pool = pool;
        Connection = connection;
    }

    public void Dispose() {
        _pool.Release(Connection);
    }
}
