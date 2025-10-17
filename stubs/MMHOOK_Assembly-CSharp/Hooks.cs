// MonoMod hook stubs for Assembly-CSharp
namespace On {
    public static class HeroController {
        public delegate void orig_Start(object self);
        public static event System.Action<orig_Start, object> Start;
        
        public delegate void orig_Update(object self);
        public static event System.Action<orig_Update, object> Update;
        
        public delegate int orig_TakeHealth(object self, int amount);
        public static event System.Func<orig_TakeHealth, object, int, int> TakeHealth;
        
        public delegate void orig_TakeDamage(object self, object go, object col, int damageAmount, int hazardType);
        public static event System.Action<orig_TakeDamage, object, object, object, int, int> TakeDamage;
    }
    
    public static class GameManager {
        public delegate void orig_Start(object self);
        public static event System.Action<orig_Start, object> Start;
        
        public delegate void orig_Update(object self);
        public static event System.Action<orig_Update, object> Update;
        
        public delegate void orig_SaveGame(object self);
        public static event System.Action<orig_SaveGame, object> SaveGame;
    }
    
    public static class UIManager {
        public delegate void orig_UIGoToDynamicMenu(object self, object menu);
        public static event System.Action<orig_UIGoToDynamicMenu, object, object> UIGoToDynamicMenu;
    }
    
    public static class InputHandler {
        public delegate void orig_Update(object self);
        public static event System.Action<orig_Update, object> Update;
    }
}

namespace On.HutongGames.PlayMaker.Actions {
    public static class SetPlayerDataBool {
        public delegate void orig_OnEnter(object self);
        public static event System.Action<orig_OnEnter, object> OnEnter;
    }
    
    public static class GetPlayerDataBool {
        public delegate void orig_OnEnter(object self);
        public static event System.Action<orig_OnEnter, object> OnEnter;
    }
}

namespace IL {
    public static class HeroController {
        public static event System.Action<object> Start;
        public static event System.Action<object> Update;
    }
    
    public static class GameManager {
        public static event System.Action<object> Start;
        public static event System.Action<object> Update;
    }
}
