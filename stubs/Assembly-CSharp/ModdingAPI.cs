// Hollow Knight Modding API stubs
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Modding {
    public interface ILogger {
        void Log(string message);
        void LogDebug(string message);
        void LogError(string message);
        void LogWarn(string message);
    }
    
    public static class Logger {
        public static void Log(string message) { }
        public static void LogDebug(string message) { }
        public static void LogError(string message) { }
        public static void LogWarn(string message) { }
    }
    
    public abstract class Mod {
        public virtual string GetName() => "Mod";
        public virtual string GetVersion() => "1.0.0";
        public virtual void Initialize() { }
        public virtual int LoadPriority() => 0;
    }
    
    public interface IGlobalSettings<T> where T : class, new() {
        void OnLoadGlobal(T s);
        T OnSaveGlobal();
    }
    
    public interface ILocalSettings<T> where T : class, new() {
        void OnLoadLocal(T s);
        T OnSaveLocal();
    }
    
    public interface ICustomMenuMod {
        bool ToggleButtonInsideMenu { get; }
        MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates);
    }
    
    public interface IMenuMod {
        // List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? toggleButtonEntry);
    }
    
    public class MenuScreen : MonoBehaviour { }
    
    public class ModToggleDelegates {
        public Action SetModEnabled;
        public Action<bool> GetModEnabled;
    }
    
    public class ModHooks {
        public static ModHooks Instance { get; } = new ModHooks();
        public event Func<string, string, string> LanguageGetHook;
        public event Action<string> SavegameLoadHook;
        public event Action BeforeSavegameSaveHook;
        public event Action AfterSavegameSaveHook;
        public event Action ApplicationQuitHook;
        public event Func<string, object> GetPlayerVariableHook;
        public event Action<string, object> SetPlayerVariableHook;
        public event Func<string, bool> GetPlayerBoolHook;
        public event Action<string, bool> SetPlayerBoolHook;
        public event Func<string, int> GetPlayerIntHook;
        public event Action<string, int> SetPlayerIntHook;
        public event Action HeroUpdateHook;
        public event Func<int, int> TakeHealthHook;
        public event Action<int> AfterTakeDamageHook;
        public event Action<GameObject, GameObject> ObjectPoolSpawnHook;
        public event Func<string, string> BeforeSceneLoadHook;
    }
    
    namespace Converters {
        public class PlayerActionSetConverter : Newtonsoft.Json.JsonConverter {
            public override bool CanConvert(Type objectType) => false;
            public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer) => null;
            public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer) { }
        }
    }
    
    namespace Menu {
        public interface IMenuMod { }
        
        namespace Config {
            public interface ICustomMenuMod { }
        }
    }
    
    namespace Utils {
        public static class ReflectionHelper {
            public static T GetField<T>(object obj, string fieldName) => default(T);
            public static void SetField(object obj, string fieldName, object value) { }
        }
    }
}

namespace InControl {
    public class PlayerAction {
        public bool IsPressed { get; set; }
        public bool WasPressed { get; set; }
        public bool WasReleased { get; set; }
        public float Value { get; set; }
    }
    
    public class PlayerActionSet {
        public PlayerAction Jump { get; set; }
        public PlayerAction Attack { get; set; }
        public PlayerAction Dash { get; set; }
        public PlayerAction Cast { get; set; }
        public PlayerAction QuickCast { get; set; }
        public PlayerAction QuickMap { get; set; }
        public PlayerAction Inventory { get; set; }
        public PlayerAction Up { get; set; }
        public PlayerAction Down { get; set; }
        public PlayerAction Left { get; set; }
        public PlayerAction Right { get; set; }
    }
    
    public class OneAxisInputControl {
        public float Value { get; set; }
    }
    
    public class TwoAxisInputControl {
        public float X { get; set; }
        public float Y { get; set; }
        public Vector2 Vector { get; set; }
    }
}

public class MenuOptionHorizontal : MonoBehaviour { }
