#!/usr/bin/env python3
import os
import re
import sys
from collections import defaultdict

# Parse all CS files to find external type references
def extract_types(directory):
    types_by_namespace = defaultdict(set)
    
    for root, dirs, files in os.walk(directory):
        for file in files:
            if not file.endswith('.cs'):
                continue
            
            filepath = os.path.join(root, file)
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()
                    
                    # Extract using directives to infer namespaces
                    using_matches = re.findall(r'using\s+([\w.]+(?:\.PlayMaker)?);', content)
                    for match in using_matches:
                        if any(ns in match for ns in ['HutongGames', 'GlobalEnums', 'Modding', 'InControl']):
                            types_by_namespace[match].add('_namespace_')
            except Exception as e:
                print(f"Error reading {filepath}: {e}", file=sys.stderr)
    
    return types_by_namespace

# Generate C# stub code for assemblies
def generate_csharp_stub_project(assembly_name, namespaces):
    project_dir = f"stubs/{assembly_name}"
    os.makedirs(project_dir, exist_ok=True)
    
    # Create csproj file
    csproj_content = f"""<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net472</TargetFramework>
        <AssemblyName>{assembly_name}</AssemblyName>
        <LangVersion>latest</LangVersion>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="UnityEngine.Modules" Version="2021.3.33" />
    </ItemGroup>
</Project>
"""
    
    with open(f"{project_dir}/{assembly_name}.csproj", 'w') as f:
        f.write(csproj_content)
    
    # Generate stub class files
    for ns in namespaces:
        cs_content = generate_namespace_stubs(ns)
        safe_name = ns.replace('.', '_')
        with open(f"{project_dir}/{safe_name}.cs", 'w') as f:
            f.write(cs_content)

def generate_namespace_stubs(namespace):
    # Common stub implementations for various namespaces
    stubs = {
        'GlobalEnums': '''
namespace GlobalEnums {
    public enum UIState { MAIN_MENU, PLAYING, PAUSED, INACTIVE }
    public enum ActorStates { no_control, grounded, idle, running, airborne, wall_sliding, hard_landing, dash_landing, previous }
    public enum HeroTransitionState { WAITING_TO_TRANSITION, WAITING_TO_TRANSITION_MID_JUMP, EXITING_SCENE, ENTERING_SCENE, DROPPING_DOWN }
    public enum MapZone { NONE, TEST_AREA, KINGS_PASS, CLIFFS, TOWN, CROSSROADS, GREEN_PATH, MINES, BONE_FOREST, WASTES, DEEPNEST, ROYAL_GARDENS, ABYSS, OUTSKIRTS, HIVE, DREAM_WORLD, RESTING_GROUNDS, WATERWAYS, WHITE_PALACE, FINAL_BOSS, SHAMAN_TEMPLE, ROYAL_GARRISONS, GODS_GLORY }
    public enum CollisionSide { top, bottom, left, right, other }
    public enum AttackTypes { Nail, Generic, Spell, RuinsWater, SharpShadow, Splatter }
    public enum DamageModifierType { DEFAULT, NONE, NAIL, SPELL }
}
''',
        'Modding': '''
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
    public class ModHooks {
        public static ModHooks Instance { get; } = new ModHooks();
        public event System.Func<string, string, string> LanguageGetHook;
        public event System.Action<string> SavegameLoadHook;
        public event System.Action BeforeSavegameSaveHook;
        public event System.Action AfterSavegameSaveHook;
        public event System.Action ApplicationQuitHook;
    }
}
''',
        'HutongGames.PlayMaker': '''
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
''',
    }
    
    return stubs.get(namespace, f'namespace {namespace} {{ public class Placeholder {{ }} }}')

if __name__ == '__main__':
    hkmp_dir = 'HKMP'
    types = extract_types(hkmp_dir)
    
    print(f"Found {len(types)} unique namespace references")
    for ns in sorted(types.keys()):
        print(f"  - {ns}")
    
    # Generate Assembly-CSharp stub (Hollow Knight specific)
    print("\nGenerating Assembly-CSharp stub...")
    generate_csharp_stub_project('Assembly-CSharp', ['GlobalEnums', 'Modding'])
    
    # Generate PlayMaker stub
    print("Generating PlayMaker stub...")
    generate_csharp_stub_project('PlayMaker', ['HutongGames.PlayMaker'])
    
    print("\nDone! Now run: cd stubs && dotnet build")
