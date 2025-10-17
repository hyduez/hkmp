
namespace HutongGames.PlayMaker {
    public class Fsm {
        public string Name { get; set; }
        public object GameObject { get; set; }
    }
    public class PlayMakerFSM : UnityEngine.MonoBehaviour {
        public string FsmName { get; set; }
        public Fsm Fsm { get; set; }
    }
    public class FsmState {
        public string Name { get; set; }
    }
    public class FsmStateAction {
        public string Name { get; set; }
    }
    public class FsmObject {
        public object Value { get; set; }
    }
    public class FsmBool {
        public bool Value { get; set; }
    }
    public class FsmInt {
        public int Value { get; set; }
    }
    public class FsmFloat {
        public float Value { get; set; }
    }
    public class FsmString {
        public string Value { get; set; }
    }
    public class FsmGameObject {
        public object Value { get; set; }
    }
    public class FsmVector2 {
        public UnityEngine.Vector2 Value { get; set; }
    }
    public class FsmVector3 {
        public UnityEngine.Vector3 Value { get; set; }
    }
}
