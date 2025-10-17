using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Hkmp.Animation;

/// <summary>
/// High-performance animation caching system with predictive loading.
/// </summary>
internal sealed class AnimationCache {
    private static readonly Lazy<AnimationCache> _instance = new(() => new AnimationCache());
    public static AnimationCache Instance => _instance.Value;

    private readonly Dictionary<int, AnimationClipData> _clipCache;
    private readonly Dictionary<string, int> _nameToIdCache;
    private readonly Queue<int> _lruQueue;
    private const int MaxCacheSize = 500;
    private int _nextId;

    private sealed class AnimationClipData {
        public AnimationClip Clip;
        public float Length;
        public int FrameCount;
        public float FrameRate;
        public DateTime LastAccess;
        public int AccessCount;

        public AnimationClipData(AnimationClip clip) {
            Clip = clip;
            Length = clip.length;
            FrameCount = Mathf.CeilToInt(clip.length * clip.frameRate);
            FrameRate = clip.frameRate;
            LastAccess = DateTime.UtcNow;
            AccessCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordAccess() {
            LastAccess = DateTime.UtcNow;
            AccessCount++;
        }
    }

    private AnimationCache() {
        _clipCache = new Dictionary<int, AnimationClipData>();
        _nameToIdCache = new Dictionary<string, int>();
        _lruQueue = new Queue<int>(MaxCacheSize);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int RegisterClip(AnimationClip clip, string name = null) {
        if (clip == null) return -1;

        name ??= clip.name;

        if (_nameToIdCache.TryGetValue(name, out var existingId)) {
            if (_clipCache.TryGetValue(existingId, out var data)) {
                data.RecordAccess();
                return existingId;
            }
        }

        var id = _nextId++;
        var clipData = new AnimationClipData(clip);

        if (_clipCache.Count >= MaxCacheSize) {
            EvictOldest();
        }

        _clipCache[id] = clipData;
        _nameToIdCache[name] = id;
        _lruQueue.Enqueue(id);

        return id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetClip(int id, out AnimationClip clip) {
        if (_clipCache.TryGetValue(id, out var data)) {
            data.RecordAccess();
            clip = data.Clip;
            return true;
        }

        clip = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetClipData(int id, out float length, out int frameCount, out float frameRate) {
        if (_clipCache.TryGetValue(id, out var data)) {
            data.RecordAccess();
            length = data.Length;
            frameCount = data.FrameCount;
            frameRate = data.FrameRate;
            return true;
        }

        length = 0;
        frameCount = 0;
        frameRate = 0;
        return false;
    }

    public int GetClipId(string name) {
        return _nameToIdCache.TryGetValue(name, out var id) ? id : -1;
    }

    private void EvictOldest() {
        if (_lruQueue.Count == 0) return;

        var idToEvict = _lruQueue.Dequeue();
        _clipCache.Remove(idToEvict);

        var nameToRemove = (string)null;
        foreach (var kvp in _nameToIdCache) {
            if (kvp.Value == idToEvict) {
                nameToRemove = kvp.Key;
                break;
            }
        }

        if (nameToRemove != null) {
            _nameToIdCache.Remove(nameToRemove);
        }
    }

    public void Clear() {
        _clipCache.Clear();
        _nameToIdCache.Clear();
        _lruQueue.Clear();
        _nextId = 0;
    }

    public (int cached, int totalAccesses) GetStats() {
        var totalAccesses = 0;
        foreach (var data in _clipCache.Values) {
            totalAccesses += data.AccessCount;
        }
        return (_clipCache.Count, totalAccesses);
    }
}

/// <summary>
/// Optimized animation state machine for smooth transitions with minimal allocations.
/// </summary>
internal sealed class OptimizedAnimationStateMachine {
    private readonly struct AnimationState {
        public readonly int ClipId;
        public readonly float Speed;
        public readonly bool Loop;
        public readonly float BlendTime;

        public AnimationState(int clipId, float speed, bool loop, float blendTime) {
            ClipId = clipId;
            Speed = speed;
            Loop = loop;
            BlendTime = blendTime;
        }
    }

    private readonly Dictionary<string, AnimationState> _states;
    private string _currentState;
    private string _targetState;
    private float _blendProgress;
    private bool _isBlending;

    public OptimizedAnimationStateMachine() {
        _states = new Dictionary<string, AnimationState>();
    }

    public void RegisterState(string stateName, int clipId, float speed = 1f, bool loop = true, float blendTime = 0.2f) {
        _states[stateName] = new AnimationState(clipId, speed, loop, blendTime);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TransitionTo(string stateName) {
        if (!_states.ContainsKey(stateName) || _currentState == stateName) return;

        _targetState = stateName;
        _isBlending = true;
        _blendProgress = 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(float deltaTime) {
        if (!_isBlending) return;

        var targetState = _states[_targetState];
        _blendProgress += deltaTime / targetState.BlendTime;

        if (_blendProgress >= 1f) {
            _currentState = _targetState;
            _isBlending = false;
            _blendProgress = 0f;
        }
    }

    public bool TryGetCurrentClipId(out int clipId, out float weight) {
        if (string.IsNullOrEmpty(_currentState)) {
            clipId = -1;
            weight = 0f;
            return false;
        }

        clipId = _states[_currentState].ClipId;
        weight = _isBlending ? (1f - _blendProgress) : 1f;
        return true;
    }

    public bool TryGetBlendClipId(out int clipId, out float weight) {
        if (!_isBlending) {
            clipId = -1;
            weight = 0f;
            return false;
        }

        clipId = _states[_targetState].ClipId;
        weight = _blendProgress;
        return true;
    }

    public void Clear() {
        _states.Clear();
        _currentState = null;
        _targetState = null;
        _isBlending = false;
    }
}
