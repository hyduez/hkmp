using UnityEngine;

namespace Hkmp.Ui;

/// <summary>
/// MonoBehaviour that updates the PlayerIndicatorManager each frame.
/// </summary>
internal class PlayerIndicatorUpdater : MonoBehaviour {
    /// <summary>
    /// The player indicator manager to update.
    /// </summary>
    private PlayerIndicatorManager _indicatorManager;

    /// <summary>
    /// Set the player indicator manager to update.
    /// </summary>
    /// <param name="indicatorManager">The manager to update.</param>
    public void SetIndicatorManager(PlayerIndicatorManager indicatorManager) {
        _indicatorManager = indicatorManager;
    }

    private void Update() {
        _indicatorManager?.Update();
    }
}
