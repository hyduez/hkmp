using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hkmp.Logging;
using Newtonsoft.Json;

namespace Hkmp.Game.Client.Save;

/// <summary>
/// High-performance save manager with compression, caching, and async I/O.
/// </summary>
internal sealed class OptimizedSaveManager : IDisposable {
    private readonly ConcurrentDictionary<string, CachedSaveData> _saveCache;
    private readonly SemaphoreSlim _saveLock;
    private readonly ArrayPool<byte> _bufferPool;
    private readonly string _saveDirectory;
    private readonly Timer _autosaveTimer;
    private readonly JsonSerializerSettings _jsonSettings;
    private volatile bool _disposed;
    private bool _isDirty;

    private sealed class CachedSaveData {
        public object Data { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime LastAccessed { get; set; }
        public bool IsDirty { get; set; }
        public int AccessCount { get; set; }
    }

    public OptimizedSaveManager(string saveDirectory, int autosaveIntervalSeconds = 300) {
        _saveCache = new ConcurrentDictionary<string, CachedSaveData>();
        _saveLock = new SemaphoreSlim(1, 1);
        _bufferPool = ArrayPool<byte>.Shared;
        _saveDirectory = saveDirectory;

        _jsonSettings = new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        Directory.CreateDirectory(_saveDirectory);

        _autosaveTimer = new Timer(
            _ => Task.Run(AutoSaveAsync),
            null,
            TimeSpan.FromSeconds(autosaveIntervalSeconds),
            TimeSpan.FromSeconds(autosaveIntervalSeconds)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get<T>(string key, T defaultValue = default) where T : class {
        if (_saveCache.TryGetValue(key, out var cached)) {
            cached.LastAccessed = DateTime.UtcNow;
            cached.AccessCount++;
            return cached.Data as T;
        }

        return defaultValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set<T>(string key, T value, bool markDirty = true) where T : class {
        var cached = _saveCache.GetOrAdd(key, _ => new CachedSaveData());
        cached.Data = value;
        cached.LastModified = DateTime.UtcNow;
        cached.LastAccessed = DateTime.UtcNow;
        cached.IsDirty = markDirty;
        cached.AccessCount++;

        if (markDirty) {
            _isDirty = true;
        }
    }

    public async Task<T> LoadAsync<T>(string key, bool useCompression = true) where T : class {
        var filePath = Path.Combine(_saveDirectory, $"{key}.save");
        
        if (!File.Exists(filePath)) {
            return null;
        }

        try {
            await _saveLock.WaitAsync();

            byte[] data;
            if (useCompression) {
                await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                using var memoryStream = new MemoryStream();
                await gzipStream.CopyToAsync(memoryStream);
                data = memoryStream.ToArray();
            } else {
                data = await File.ReadAllBytesAsync(filePath);
            }

            var json = Encoding.UTF8.GetString(data);
            var result = JsonConvert.DeserializeObject<T>(json, _jsonSettings);

            Set(key, result, markDirty: false);

            return result;
        } catch (Exception ex) {
            Logger.Error($"Error loading save '{key}': {ex}");
            return null;
        } finally {
            _saveLock.Release();
        }
    }

    public async Task<bool> SaveAsync<T>(string key, T data, bool useCompression = true) where T : class {
        if (data == null) return false;

        var filePath = Path.Combine(_saveDirectory, $"{key}.save");
        var tempPath = filePath + ".tmp";

        try {
            await _saveLock.WaitAsync();

            var json = JsonConvert.SerializeObject(data, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            if (useCompression) {
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
                await gzipStream.WriteAsync(bytes, 0, bytes.Length);
            } else {
                await File.WriteAllBytesAsync(tempPath, bytes);
            }

            if (File.Exists(filePath)) {
                File.Replace(tempPath, filePath, null);
            } else {
                File.Move(tempPath, filePath);
            }

            Set(key, data, markDirty: false);

            return true;
        } catch (Exception ex) {
            Logger.Error($"Error saving '{key}': {ex}");
            
            if (File.Exists(tempPath)) {
                try {
                    File.Delete(tempPath);
                } catch {
                    // Ignore
                }
            }

            return false;
        } finally {
            _saveLock.Release();
        }
    }

    private async Task AutoSaveAsync() {
        if (!_isDirty || _disposed) return;

        Logger.Debug("Starting autosave...");
        var savedCount = 0;

        foreach (var kvp in _saveCache) {
            if (kvp.Value.IsDirty && kvp.Value.Data != null) {
                if (await SaveAsync(kvp.Key, kvp.Value.Data)) {
                    kvp.Value.IsDirty = false;
                    savedCount++;
                }
            }
        }

        if (savedCount > 0) {
            Logger.Info($"Autosaved {savedCount} file(s)");
            _isDirty = false;
        }
    }

    public async Task SaveAllAsync() {
        foreach (var kvp in _saveCache) {
            if (kvp.Value.Data != null) {
                await SaveAsync(kvp.Key, kvp.Value.Data);
            }
        }
        _isDirty = false;
    }

    public bool Delete(string key) {
        var filePath = Path.Combine(_saveDirectory, $"{key}.save");
        
        try {
            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }

            _saveCache.TryRemove(key, out _);
            return true;
        } catch (Exception ex) {
            Logger.Error($"Error deleting save '{key}': {ex}");
            return false;
        }
    }

    public void ClearCache() {
        _saveCache.Clear();
        _isDirty = false;
    }

    public (int cached, int dirty, int totalAccesses) GetStats() {
        var dirtyCount = 0;
        var totalAccesses = 0;

        foreach (var cached in _saveCache.Values) {
            if (cached.IsDirty) dirtyCount++;
            totalAccesses += cached.AccessCount;
        }

        return (_saveCache.Count, dirtyCount, totalAccesses);
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;

        _autosaveTimer?.Dispose();
        
        if (_isDirty) {
            SaveAllAsync().GetAwaiter().GetResult();
        }

        _saveLock?.Dispose();
    }
}
