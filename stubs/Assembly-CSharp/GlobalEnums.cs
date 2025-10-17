
namespace GlobalEnums {
    public enum UIState { MAIN_MENU, PLAYING, PAUSED, INACTIVE }
    public enum ActorStates { no_control, grounded, idle, running, airborne, wall_sliding, hard_landing, dash_landing, previous }
    public enum HeroTransitionState { WAITING_TO_TRANSITION, WAITING_TO_TRANSITION_MID_JUMP, EXITING_SCENE, ENTERING_SCENE, DROPPING_DOWN }
    public enum MapZone { NONE, TEST_AREA, KINGS_PASS, CLIFFS, TOWN, CROSSROADS, GREEN_PATH, MINES, BONE_FOREST, WASTES, DEEPNEST, ROYAL_GARDENS, ABYSS, OUTSKIRTS, HIVE, DREAM_WORLD, RESTING_GROUNDS, WATERWAYS, WHITE_PALACE, FINAL_BOSS, SHAMAN_TEMPLE, ROYAL_GARRISONS, GODS_GLORY }
    public enum CollisionSide { top, bottom, left, right, other }
    public enum AttackTypes { Nail, Generic, Spell, RuinsWater, SharpShadow, Splatter }
    public enum DamageModifierType { DEFAULT, NONE, NAIL, SPELL }
}
