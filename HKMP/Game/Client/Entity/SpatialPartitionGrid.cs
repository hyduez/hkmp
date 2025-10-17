using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Hkmp.Game.Client.Entity;

/// <summary>
/// Spatial partitioning grid for efficient entity lookups and culling.
/// Dramatically improves performance when dealing with many entities.
/// </summary>
internal sealed class SpatialPartitionGrid<T> where T : class {
    private readonly Dictionary<long, List<Entry>> _grid;
    private readonly float _cellSize;
    private readonly int _gridWidth;
    private readonly int _gridHeight;
    
    private struct Entry {
        public T Item;
        public Vector2 Position;
        public long CellKey;

        public Entry(T item, Vector2 position, long cellKey) {
            Item = item;
            Position = position;
            CellKey = cellKey;
        }
    }

    private readonly Dictionary<T, Entry> _itemToEntry;

    public SpatialPartitionGrid(float cellSize = 10f, int width = 1000, int height = 1000) {
        _cellSize = cellSize;
        _gridWidth = (int)(width / cellSize);
        _gridHeight = (int)(height / cellSize);
        _grid = new Dictionary<long, List<Entry>>();
        _itemToEntry = new Dictionary<T, Entry>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long GetCellKey(Vector2 position) {
        var x = Mathf.FloorToInt(position.x / _cellSize);
        var y = Mathf.FloorToInt(position.y / _cellSize);
        return ((long)x << 32) | (uint)y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (int x, int y) GetCellCoords(long cellKey) {
        var x = (int)(cellKey >> 32);
        var y = (int)(cellKey & 0xFFFFFFFF);
        return (x, y);
    }

    public void Insert(T item, Vector2 position) {
        if (_itemToEntry.ContainsKey(item)) {
            Update(item, position);
            return;
        }

        var cellKey = GetCellKey(position);
        var entry = new Entry(item, position, cellKey);

        if (!_grid.TryGetValue(cellKey, out var cell)) {
            cell = new List<Entry>(8);
            _grid[cellKey] = cell;
        }

        cell.Add(entry);
        _itemToEntry[item] = entry;
    }

    public void Update(T item, Vector2 newPosition) {
        if (!_itemToEntry.TryGetValue(item, out var oldEntry)) return;

        var newCellKey = GetCellKey(newPosition);
        
        if (oldEntry.CellKey == newCellKey) {
            oldEntry.Position = newPosition;
            _itemToEntry[item] = oldEntry;
            return;
        }

        Remove(item);
        Insert(item, newPosition);
    }

    public bool Remove(T item) {
        if (!_itemToEntry.TryGetValue(item, out var entry)) return false;

        if (_grid.TryGetValue(entry.CellKey, out var cell)) {
            cell.RemoveAll(e => ReferenceEquals(e.Item, item));
            if (cell.Count == 0) {
                _grid.Remove(entry.CellKey);
            }
        }

        _itemToEntry.Remove(item);
        return true;
    }

    public void Query(Vector2 position, float radius, List<T> results) {
        results.Clear();

        var radiusSq = radius * radius;
        var cellRadius = Mathf.CeilToInt(radius / _cellSize);

        var centerCellKey = GetCellKey(position);
        var (centerX, centerY) = GetCellCoords(centerCellKey);

        for (var dx = -cellRadius; dx <= cellRadius; dx++) {
            for (var dy = -cellRadius; dy <= cellRadius; dy++) {
                var cellKey = ((long)(centerX + dx) << 32) | (uint)(centerY + dy);
                
                if (!_grid.TryGetValue(cellKey, out var cell)) continue;

                foreach (var entry in cell) {
                    var distSq = Vector2.SqrMagnitude(entry.Position - position);
                    if (distSq <= radiusSq) {
                        results.Add(entry.Item);
                    }
                }
            }
        }
    }

    public void QueryRect(Rect rect, List<T> results) {
        results.Clear();

        var minCellKey = GetCellKey(rect.min);
        var maxCellKey = GetCellKey(rect.max);

        var (minX, minY) = GetCellCoords(minCellKey);
        var (maxX, maxY) = GetCellCoords(maxCellKey);

        for (var x = minX; x <= maxX; x++) {
            for (var y = minY; y <= maxY; y++) {
                var cellKey = ((long)x << 32) | (uint)y;
                
                if (!_grid.TryGetValue(cellKey, out var cell)) continue;

                foreach (var entry in cell) {
                    if (rect.Contains(entry.Position)) {
                        results.Add(entry.Item);
                    }
                }
            }
        }
    }

    public void GetNearestNeighbor(Vector2 position, out T nearest, out float distance) {
        nearest = null;
        distance = float.MaxValue;

        var cellKey = GetCellKey(position);
        var (centerX, centerY) = GetCellCoords(cellKey);

        var searchRadius = 1;
        const int maxSearchRadius = 10;

        while (nearest == null && searchRadius <= maxSearchRadius) {
            for (var dx = -searchRadius; dx <= searchRadius; dx++) {
                for (var dy = -searchRadius; dy <= searchRadius; dy++) {
                    if (System.Math.Abs(dx) != searchRadius && System.Math.Abs(dy) != searchRadius) {
                        continue;
                    }

                    var cellKey2 = ((long)(centerX + dx) << 32) | (uint)(centerY + dy);
                    
                    if (!_grid.TryGetValue(cellKey2, out var cell)) continue;

                    foreach (var entry in cell) {
                        var dist = Vector2.Distance(entry.Position, position);
                        if (dist < distance) {
                            distance = dist;
                            nearest = entry.Item;
                        }
                    }
                }
            }

            searchRadius++;
        }
    }

    public void Clear() {
        _grid.Clear();
        _itemToEntry.Clear();
    }

    public int Count => _itemToEntry.Count;
}
