using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hkmp.Logging;

/// <summary>
/// High-performance asynchronous logger with structured logging and minimal allocations.
/// </summary>
internal sealed class FastLogger : IDisposable {
    private readonly ConcurrentQueue<LogEntry> _logQueue;
    private readonly SemaphoreSlim _logSemaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _logWorker;
    private readonly string _logFilePath;
    private readonly LogLevel _minLevel;
    private readonly int _maxQueueSize;
    private volatile bool _disposed;

    private readonly struct LogEntry {
        public readonly LogLevel Level;
        public readonly string Message;
        public readonly string Source;
        public readonly DateTime Timestamp;
        public readonly Exception Exception;

        public LogEntry(LogLevel level, string message, string source, Exception exception = null) {
            Level = level;
            Message = message;
            Source = source;
            Timestamp = DateTime.UtcNow;
            Exception = exception;
        }
    }

    public FastLogger(string logFilePath = null, LogLevel minLevel = LogLevel.Info, int maxQueueSize = 10000) {
        _logQueue = new ConcurrentQueue<LogEntry>();
        _logSemaphore = new SemaphoreSlim(0);
        _cancellationTokenSource = new CancellationTokenSource();
        _logFilePath = logFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), "hkmp.log");
        _minLevel = minLevel;
        _maxQueueSize = maxQueueSize;

        _logWorker = Task.Run(LogWorkerAsync);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Log(LogLevel level, string message, [CallerMemberName] string source = "", Exception exception = null) {
        if (level < _minLevel || _disposed || _logQueue.Count >= _maxQueueSize) return;

        var entry = new LogEntry(level, message, source, exception);
        _logQueue.Enqueue(entry);
        _logSemaphore.Release();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Debug(string message, [CallerMemberName] string source = "") =>
        Log(LogLevel.Debug, message, source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Info(string message, [CallerMemberName] string source = "") =>
        Log(LogLevel.Info, message, source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Warn(string message, [CallerMemberName] string source = "") =>
        Log(LogLevel.Warning, message, source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Error(string message, [CallerMemberName] string source = "", Exception exception = null) =>
        Log(LogLevel.Error, message, source, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fatal(string message, [CallerMemberName] string source = "", Exception exception = null) =>
        Log(LogLevel.Fatal, message, source, exception);

    private async Task LogWorkerAsync() {
        var token = _cancellationTokenSource.Token;
        var buffer = new StringBuilder(1024);

        using var fileStream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, true);
        using var writer = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = false };

        while (!token.IsCancellationRequested) {
            try {
                await _logSemaphore.WaitAsync(100, token);

                var batchSize = 0;
                buffer.Clear();

                while (batchSize < 100 && _logQueue.TryDequeue(out var entry)) {
                    FormatLogEntry(buffer, entry);
                    batchSize++;
                }

                if (batchSize > 0) {
                    await writer.WriteAsync(buffer.ToString());
                    await writer.FlushAsync();
                }
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                Debug.WriteLine($"Error in log worker: {ex}");
            }
        }

        while (_logQueue.TryDequeue(out var entry)) {
            buffer.Clear();
            FormatLogEntry(buffer, entry);
            await writer.WriteAsync(buffer.ToString());
        }

        await writer.FlushAsync();
    }

    private static void FormatLogEntry(StringBuilder buffer, LogEntry entry) {
        buffer.Append('[');
        buffer.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        buffer.Append("] [");
        buffer.Append(GetLevelString(entry.Level));
        buffer.Append("] [");
        buffer.Append(entry.Source);
        buffer.Append("] ");
        buffer.Append(entry.Message);

        if (entry.Exception != null) {
            buffer.AppendLine();
            buffer.Append("Exception: ");
            buffer.Append(entry.Exception);
        }

        buffer.AppendLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetLevelString(LogLevel level) => level switch {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Fatal => "FATAL",
        _ => "UNKNW"
    };

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;

        _cancellationTokenSource.Cancel();
        
        try {
            _logWorker.Wait(TimeSpan.FromSeconds(5));
        } catch {
            // Ignore
        }

        _logSemaphore?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

internal enum LogLevel {
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Fatal = 4
}
