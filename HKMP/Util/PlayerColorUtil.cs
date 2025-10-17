using UnityEngine;

namespace Hkmp.Util;

/// <summary>
/// Utility class for generating unique colors for players based on their ID.
/// </summary>
internal static class PlayerColorUtil {
    /// <summary>
    /// Predefined color palette for players. These colors are chosen to be distinct and visible.
    /// </summary>
    private static readonly Color[] ColorPalette = {
        new Color(1.0f, 0.2f, 0.2f),      // Red
        new Color(0.2f, 0.8f, 0.2f),      // Green
        new Color(0.3f, 0.6f, 1.0f),      // Blue
        new Color(1.0f, 1.0f, 0.2f),      // Yellow
        new Color(1.0f, 0.4f, 1.0f),      // Magenta
        new Color(0.3f, 1.0f, 1.0f),      // Cyan
        new Color(1.0f, 0.6f, 0.2f),      // Orange
        new Color(0.6f, 0.3f, 1.0f),      // Purple
        new Color(1.0f, 0.8f, 0.8f),      // Light Pink
        new Color(0.5f, 1.0f, 0.5f),      // Light Green
        new Color(1.0f, 1.0f, 0.8f),      // Light Yellow
        new Color(0.8f, 0.5f, 0.3f),      // Brown
        new Color(0.8f, 0.8f, 1.0f),      // Light Blue
        new Color(1.0f, 0.5f, 0.7f),      // Rose
        new Color(0.5f, 1.0f, 0.8f),      // Mint
        new Color(0.9f, 0.7f, 1.0f),      // Lavender
    };

    /// <summary>
    /// Get a unique color for a player based on their ID.
    /// Uses a predefined palette that cycles, with slight variations for IDs beyond the palette size.
    /// </summary>
    /// <param name="playerId">The player ID.</param>
    /// <returns>A unique Color for the player.</returns>
    public static Color GetPlayerColor(ushort playerId) {
        // Use the palette for the first set of colors
        var paletteIndex = playerId % ColorPalette.Length;
        var baseColor = ColorPalette[paletteIndex];
        
        // If we've cycled through the palette, add slight variations
        var cycle = playerId / ColorPalette.Length;
        if (cycle > 0) {
            // Generate a slight variation based on the cycle
            var hue = (cycle * 0.13f) % 1.0f; // Offset hue slightly
            Color.RGBToHSV(baseColor, out var h, out var s, out var v);
            
            // Adjust hue slightly but keep saturation and value
            h = (h + hue * 0.15f) % 1.0f;
            baseColor = Color.HSVToRGB(h, s, v);
        }
        
        return baseColor;
    }
}
