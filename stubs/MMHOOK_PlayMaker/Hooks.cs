// MonoMod hook stubs for PlayMaker
namespace On.HutongGames.PlayMaker {
    public static class Fsm {
        public delegate void orig_Start(object self);
        public static event System.Action<orig_Start, object> Start;
    }
    
    public static class PlayMakerFSM {
        public delegate void orig_Start(object self);
        public static event System.Action<orig_Start, object> Start;
        
        public delegate void orig_OnEnable(object self);
        public static event System.Action<orig_OnEnable, object> OnEnable;
    }
}

namespace IL.HutongGames.PlayMaker {
    public static class Fsm {
        public static event System.Action<object> Start;
    }
}
