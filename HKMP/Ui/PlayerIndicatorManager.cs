using System.Collections.Generic;
using System.Linq;
using Hkmp.Game.Client;
using Hkmp.Util;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hkmp.Ui;

/// <summary>
/// Manager for player position indicators (arrows pointing to off-screen players).
/// </summary>
internal class PlayerIndicatorManager {
    /// <summary>
    /// The size of the indicator arrows.
    /// </summary>
    private const float IndicatorSize = 40f;
    
    /// <summary>
    /// The margin from the edge of the screen.
    /// </summary>
    private const float ScreenEdgeMargin = 50f;
    
    /// <summary>
    /// Reference to the player data dictionary.
    /// </summary>
    private readonly Dictionary<ushort, ClientPlayerData> _playerData;
    
    /// <summary>
    /// Dictionary mapping player IDs to their indicator GameObjects.
    /// </summary>
    private readonly Dictionary<ushort, GameObject> _indicators;
    
    /// <summary>
    /// The parent GameObject for all indicators.
    /// </summary>
    private GameObject _indicatorParent;
    
    /// <summary>
    /// Whether the indicator system is enabled.
    /// </summary>
    private bool _isEnabled;

    public PlayerIndicatorManager(Dictionary<ushort, ClientPlayerData> playerData) {
        _playerData = playerData;
        _indicators = new Dictionary<ushort, GameObject>();
    }

    /// <summary>
    /// Initialize the indicator manager.
    /// </summary>
    public void Initialize() {
        _indicatorParent = new GameObject("PlayerIndicators");
        _indicatorParent.transform.SetParent(UiManager.UiGameObject.transform, false);
        
        var rectTransform = _indicatorParent.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        
        _isEnabled = true;
    }

    /// <summary>
    /// Update the indicators for all players. Should be called each frame.
    /// </summary>
    public void Update() {
        if (!_isEnabled || !_indicatorParent) {
            return;
        }
        
        var heroController = HeroController.instance;
        if (!heroController) {
            // Hide all indicators if there's no hero controller
            foreach (var indicator in _indicators.Values) {
                if (indicator) {
                    indicator.SetActive(false);
                }
            }
            return;
        }

        // Get the main camera
        Camera camera = null;
        if (GameCameras.instance != null) {
            camera = GameCameras.instance.mainCamera;
        }
        
        if (!camera) {
            return;
        }

        var activePlayerIds = new HashSet<ushort>();

        foreach (var kvp in _playerData) {
            var playerId = kvp.Key;
            var playerData = kvp.Value;

            if (!playerData.IsInLocalScene || !playerData.PlayerContainer) {
                // Hide indicator if player is not in scene
                if (_indicators.TryGetValue(playerId, out var indicator) && indicator) {
                    indicator.SetActive(false);
                }
                continue;
            }

            activePlayerIds.Add(playerId);
            
            var playerPosition = playerData.PlayerContainer.transform.position;
            var screenPosition = camera.WorldToViewportPoint(playerPosition);
            
            // Check if player is off-screen
            bool isOffScreen = screenPosition.x < 0 || screenPosition.x > 1 || 
                              screenPosition.y < 0 || screenPosition.y > 1 ||
                              screenPosition.z < 0;

            if (!_indicators.TryGetValue(playerId, out var indicatorObj)) {
                // Create new indicator
                indicatorObj = CreateIndicator(playerId);
                _indicators[playerId] = indicatorObj;
            }

            if (isOffScreen) {
                // Show and update indicator
                indicatorObj.SetActive(true);
                UpdateIndicatorPosition(indicatorObj, screenPosition, playerPosition, camera);
            } else {
                // Hide indicator if player is on screen
                indicatorObj.SetActive(false);
            }
        }

        // Clean up indicators for players no longer in the scene
        var toRemove = _indicators.Keys.Where(id => !activePlayerIds.Contains(id)).ToList();
        foreach (var id in toRemove) {
            if (_indicators.TryGetValue(id, out var indicator) && indicator) {
                Object.Destroy(indicator);
            }
            _indicators.Remove(id);
        }
    }

    /// <summary>
    /// Create a new indicator GameObject for a player.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <returns>The created indicator GameObject.</returns>
    private GameObject CreateIndicator(ushort playerId) {
        var indicator = new GameObject($"PlayerIndicator_{playerId}");
        indicator.transform.SetParent(_indicatorParent.transform, false);
        
        var rectTransform = indicator.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(IndicatorSize, IndicatorSize);
        
        // Create arrow image
        var image = indicator.AddComponent<Image>();
        
        // Create a simple triangle sprite for the arrow
        var texture = new Texture2D(32, 32);
        var pixels = new Color[32 * 32];
        
        // Draw a simple triangle pointing up
        for (int y = 0; y < 32; y++) {
            for (int x = 0; x < 32; x++) {
                // Triangle shape: pointing upward
                float normalizedY = y / 32f;
                float normalizedX = x / 32f;
                float centerX = 0.5f;
                float triangleWidth = (1f - normalizedY) * 0.8f;
                
                bool isInTriangle = normalizedY > 0.2f && 
                                   Mathf.Abs(normalizedX - centerX) < triangleWidth / 2f;
                
                pixels[y * 32 + x] = isInTriangle ? Color.white : Color.clear;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, 32, 32),
            new Vector2(0.5f, 0.5f)
        );
        
        image.sprite = sprite;
        
        // Set the color based on player ID
        image.color = GetPlayerColor(playerId);
        
        indicator.SetActive(false);
        
        return indicator;
    }

    /// <summary>
    /// Update the position and rotation of an indicator.
    /// </summary>
    /// <param name="indicator">The indicator GameObject.</param>
    /// <param name="screenPosition">The screen position of the player.</param>
    /// <param name="worldPosition">The world position of the player.</param>
    /// <param name="camera">The game camera.</param>
    private void UpdateIndicatorPosition(GameObject indicator, Vector3 screenPosition, Vector3 worldPosition, Camera camera) {
        var rectTransform = indicator.GetComponent<RectTransform>();
        
        // Clamp screen position to edges
        var clampedX = Mathf.Clamp(screenPosition.x, 0.05f, 0.95f);
        var clampedY = Mathf.Clamp(screenPosition.y, 0.05f, 0.95f);
        
        // Convert to anchor position (0-1 range for screen)
        rectTransform.anchorMin = new Vector2(clampedX, clampedY);
        rectTransform.anchorMax = new Vector2(clampedX, clampedY);
        rectTransform.anchoredPosition = Vector2.zero;
        
        // Calculate direction to player for rotation
        var heroPos = HeroController.instance.transform.position;
        var directionToPlayer = (worldPosition - heroPos).normalized;
        
        // Calculate angle
        var angle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;
        
        // Rotate the arrow to point towards the player (adjust by -90 because sprite points up by default)
        rectTransform.rotation = Quaternion.Euler(0, 0, angle - 90f);
    }

    /// <summary>
    /// Get a unique color for a player based on their ID.
    /// </summary>
    /// <param name="playerId">The player ID.</param>
    /// <returns>A Color for the player.</returns>
    private Color GetPlayerColor(ushort playerId) {
        // Use the same color generation as the player names
        return PlayerColorUtil.GetPlayerColor(playerId);
    }

    /// <summary>
    /// Enable or disable the indicator system.
    /// </summary>
    /// <param name="enabled">Whether the system should be enabled.</param>
    public void SetEnabled(bool enabled) {
        _isEnabled = enabled;
        
        if (!enabled && _indicatorParent) {
            // Hide all indicators
            foreach (var indicator in _indicators.Values) {
                if (indicator) {
                    indicator.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Clean up all indicators.
    /// </summary>
    public void Destroy() {
        foreach (var indicator in _indicators.Values) {
            if (indicator) {
                Object.Destroy(indicator);
            }
        }
        _indicators.Clear();
        
        if (_indicatorParent) {
            Object.Destroy(_indicatorParent);
        }
    }
}
