using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HutongGames.PlayMaker;
using UnityEngine;

namespace Hkmp.Fsm;

/// <summary>
/// High-performance FSM caching system to avoid expensive GameObject.Find and FSM lookups.
/// </summary>
internal sealed class OptimizedFsmCache {
    private static readonly Lazy<OptimizedFsmCache> _instance = new(() => new OptimizedFsmCache());
    public static OptimizedFsmCache Instance => _instance.Value;

    private readonly Dictionary<int, PlayMakerFSM[]> _gameObjectFsmCache;
    private readonly Dictionary<(int, string), PlayMakerFSM> _namedFsmCache;
    private readonly Dictionary<(int, string, string), FsmState> _fsmStateCache;
    private readonly Dictionary<(int, string, string, int), FsmStateAction> _fsmActionCache;
    private readonly HashSet<int> _invalidatedObjects;
    
    private const int MaxCacheSize = 5000;

    private OptimizedFsmCache() {
        _gameObjectFsmCache = new Dictionary<int, PlayMakerFSM[]>();
        _namedFsmCache = new Dictionary<(int, string), PlayMakerFSM>();
        _fsmStateCache = new Dictionary<(int, string, string), FsmState>();
        _fsmActionCache = new Dictionary<(int, string, string, int), FsmStateAction>();
        _invalidatedObjects = new HashSet<int>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PlayMakerFSM[] GetFsms(GameObject gameObject) {
        if (gameObject == null) return Array.Empty<PlayMakerFSM>();

        var id = gameObject.GetInstanceID();
        
        if (_invalidatedObjects.Contains(id)) {
            InvalidateGameObject(id);
            _invalidatedObjects.Remove(id);
        }

        if (_gameObjectFsmCache.TryGetValue(id, out var fsms)) {
            return fsms;
        }

        fsms = gameObject.GetComponents<PlayMakerFSM>();
        
        if (_gameObjectFsmCache.Count < MaxCacheSize) {
            _gameObjectFsmCache[id] = fsms;
        }

        return fsms;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PlayMakerFSM GetFsm(GameObject gameObject, string fsmName) {
        if (gameObject == null || string.IsNullOrEmpty(fsmName)) return null;

        var id = gameObject.GetInstanceID();
        var key = (id, fsmName);

        if (_namedFsmCache.TryGetValue(key, out var fsm)) {
            return fsm;
        }

        var fsms = GetFsms(gameObject);
        foreach (var f in fsms) {
            if (f.FsmName == fsmName) {
                if (_namedFsmCache.Count < MaxCacheSize) {
                    _namedFsmCache[key] = f;
                }
                return f;
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FsmState GetState(GameObject gameObject, string fsmName, string stateName) {
        if (gameObject == null || string.IsNullOrEmpty(fsmName) || string.IsNullOrEmpty(stateName)) {
            return null;
        }

        var id = gameObject.GetInstanceID();
        var key = (id, fsmName, stateName);

        if (_fsmStateCache.TryGetValue(key, out var state)) {
            return state;
        }

        var fsm = GetFsm(gameObject, fsmName);
        if (fsm == null) return null;

        var states = fsm.FsmStates;
        foreach (var s in states) {
            if (s.Name == stateName) {
                if (_fsmStateCache.Count < MaxCacheSize) {
                    _fsmStateCache[key] = s;
                }
                return s;
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetAction<T>(GameObject gameObject, string fsmName, string stateName, int actionIndex) 
        where T : FsmStateAction {
        
        if (gameObject == null) return null;

        var id = gameObject.GetInstanceID();
        var key = (id, fsmName, stateName, actionIndex);

        if (_fsmActionCache.TryGetValue(key, out var action)) {
            return action as T;
        }

        var state = GetState(gameObject, fsmName, stateName);
        if (state == null || actionIndex < 0 || actionIndex >= state.Actions.Length) {
            return null;
        }

        var foundAction = state.Actions[actionIndex];
        
        if (foundAction is T typedAction) {
            if (_fsmActionCache.Count < MaxCacheSize) {
                _fsmActionCache[key] = foundAction;
            }
            return typedAction;
        }

        return null;
    }

    public void InvalidateGameObject(int instanceId) {
        _gameObjectFsmCache.Remove(instanceId);

        var keysToRemove = new List<(int, string)>();
        foreach (var key in _namedFsmCache.Keys) {
            if (key.Item1 == instanceId) {
                keysToRemove.Add(key);
            }
        }
        foreach (var key in keysToRemove) {
            _namedFsmCache.Remove(key);
        }

        var stateKeysToRemove = new List<(int, string, string)>();
        foreach (var key in _fsmStateCache.Keys) {
            if (key.Item1 == instanceId) {
                stateKeysToRemove.Add(key);
            }
        }
        foreach (var key in stateKeysToRemove) {
            _fsmStateCache.Remove(key);
        }

        var actionKeysToRemove = new List<(int, string, string, int)>();
        foreach (var key in _fsmActionCache.Keys) {
            if (key.Item1 == instanceId) {
                actionKeysToRemove.Add(key);
            }
        }
        foreach (var key in actionKeysToRemove) {
            _fsmActionCache.Remove(key);
        }
    }

    public void MarkForInvalidation(GameObject gameObject) {
        if (gameObject != null) {
            _invalidatedObjects.Add(gameObject.GetInstanceID());
        }
    }

    public void Clear() {
        _gameObjectFsmCache.Clear();
        _namedFsmCache.Clear();
        _fsmStateCache.Clear();
        _fsmActionCache.Clear();
        _invalidatedObjects.Clear();
    }

    public (int fsms, int named, int states, int actions) GetCacheStats() {
        return (
            _gameObjectFsmCache.Count,
            _namedFsmCache.Count,
            _fsmStateCache.Count,
            _fsmActionCache.Count
        );
    }
}
