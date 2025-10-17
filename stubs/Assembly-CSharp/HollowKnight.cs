// Hollow Knight game classes stubs
using UnityEngine;

// Main game classes
public class HeroController : MonoBehaviour {
    public static HeroController instance;
    public HeroAnimationController animCtrl;
    public Transform transform;
    public GameObject gameObject;
    public Rigidbody2D rb2d;
    public PlayerData playerData;
    public bool acceptingInput;
    public virtual void Start() { }
    public virtual void Update() { }
    public virtual int TakeHealth(int amount) { return 0; }
    public virtual void TakeDamage(GameObject source, CollisionSide collideSide, int damageAmount, int hazardType) { }
    public virtual void AddMPCharge(int amount) { }
}

public class HeroAnimationController : MonoBehaviour {
    public virtual void Play(string clipName) { }
    public virtual void PlayClip(string clipName) { }
}

public class GameManager : MonoBehaviour {
    public static GameManager instance;
    public PlayerData playerData;
    public UIManager ui;
    public SceneData sceneData;
    public string sceneName;
    public virtual void Start() { }
    public virtual void Update() { }
    public virtual void SaveGame() { }
    public virtual void BeginSceneTransition(object info) { }
}

public class PlayerData : MonoBehaviour {
    public static PlayerData instance;
    public int health;
    public int maxHealth;
    public int MPCharge;
    public int maxMP;
    public string respawnScene;
    public float respawnMarkerX;
    public float respawnMarkerY;
    public bool equippedCharm_1;
    public bool GetBool(string name) { return false; }
    public void SetBool(string name, bool value) { }
    public int GetInt(string name) { return 0; }
    public void SetInt(string name, int value) { }
    public float GetFloat(string name) { return 0f; }
    public void SetFloat(string name, float value) { }
    public string GetString(string name) { return ""; }
    public void SetString(string name, string value) { }
}

public class UIManager : MonoBehaviour {
    public static UIManager instance;
    public virtual void UIGoToDynamicMenu(GameObject menu) { }
    public virtual void UIClosePauseMenu() { }
    public virtual void UIGoToMainMenu() { }
}

public class InputHandler : MonoBehaviour {
    public static InputHandler instance;
    public InControl.PlayerActionSet inputActions;
    public virtual void Update() { }
}

public class HealthManager : MonoBehaviour {
    public int hp;
    public virtual void Die(float? attackDirection, GlobalEnums.AttackTypes attackType, bool ignoreEvasion) { }
    public virtual void Hit(object hitInstance) { }
}

public class DamageHero : MonoBehaviour {
    public int damageDealt;
    public GlobalEnums.AttackTypes attackType;
    public bool resetOnEnable;
    public object shadowDashHazard;
}

public class HealthManagerExt : MonoBehaviour { }

public class SceneData : MonoBehaviour {
    public string sceneName;
}

public class GameMap : MonoBehaviour {
    public static GameMap instance;
}

public class BossSequence : MonoBehaviour { }

public class BossStatue : MonoBehaviour {
    public object statueState;
    public string bossScene;
    public string bossName;
}

public class BossSequenceDoor : MonoBehaviour {
    public object statueState;
}

public class BridgeLever : MonoBehaviour { }
public class Climber : MonoBehaviour { }
public class DreamPlatform : MonoBehaviour { }
public class EndGGBossScene : MonoBehaviour { }
public class EnemySpawner : MonoBehaviour { }
public class FlipPlatform : MonoBehaviour { }
public class HazardRespawnTrigger : MonoBehaviour { }
public class IgnoreHeroCollision : MonoBehaviour { }
public class PreBuildTK2DSprites : MonoBehaviour { }
public class PreSpawnGameObjects : MonoBehaviour { }
public class TeleportAfterTimer : MonoBehaviour { }

public enum CollisionSide {
    top, bottom, left, right, other
}

public enum HazardType {
    NON_HAZARD,
    SPIKES,
    ACID,
    LAVA,
    PIT,
    ENEMY_ATTACK
}

public class SceneLoad : MonoBehaviour { }
public class SetTextMeshProGameText : MonoBehaviour { }
public class StartSceneButton : MonoBehaviour { }
public class TransitionPoint : MonoBehaviour { }

public class MusicCue : ScriptableObject {
    public object originalMusicCue;
    public string originalMusicEventName;
    public float delayTime;
}

// Jetbrains annotations
namespace JetBrains.Annotations {
    [System.AttributeUsage(System.AttributeTargets.All)]
    public class CanBeNullAttribute : System.Attribute { }
    
    [System.AttributeUsage(System.AttributeTargets.All)]
    public class NotNullAttribute : System.Attribute { }
}
