#!/usr/bin/env dotnet-script

#r "nuget: Mono.Cecil, 0.11.6"

using Mono.Cecil;
using System.IO;

var outputDir = Args.FirstOrDefault() ?? "./HKMP/lib";
Directory.CreateDirectory(outputDir);

void CreateAssembly(string name, Action<ModuleDefinition> populate) {
    var assembly = AssemblyDefinition.CreateAssembly(
        new AssemblyNameDefinition(name, new Version(0, 0, 0, 0)),
        name,
        ModuleKind.Dll
    );
    
    populate(assembly.MainModule);
    
    var outputPath = Path.Combine(outputDir, $"{name}.dll");
    assembly.Write(outputPath);
    Console.WriteLine($"Created {outputPath}");
}

// Create Assembly-CSharp stub
CreateAssembly("Assembly-CSharp", module => {
    // Add GlobalEnums namespace
    var globalEnumsType = new TypeDefinition("GlobalEnums", "UIState", TypeAttributes.Public | TypeAttributes.Class);
    module.Types.Add(globalEnumsType);
    
    // Add other common Hollow Knight types as needed
    var heroControllerType = new TypeDefinition("", "HeroController", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
    module.Types.Add(heroControllerType);
    
    var gameManagerType = new TypeDefinition("", "GameManager", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
    module.Types.Add(gameManagerType);
});

// Create MMHOOK assemblies
CreateAssembly("MMHOOK_Assembly-CSharp", module => {
    var onType = new TypeDefinition("On", "HeroController", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
    module.Types.Add(onType);
});

CreateAssembly("MMHOOK_PlayMaker", module => {
    // Minimal MMHook stub
});

// Create PlayMaker stub
CreateAssembly("PlayMaker", module => {
    var fsmType = new TypeDefinition("HutongGames.PlayMaker", "Fsm", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
    module.Types.Add(fsmType);
    
    var playMakerFSMType = new TypeDefinition("HutongGames.PlayMaker", "PlayMakerFSM", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
    module.Types.Add(playMakerFSMType);
});

Console.WriteLine("Stub assemblies created successfully!");
