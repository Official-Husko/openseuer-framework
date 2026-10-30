using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ObenLoader.Mods;

namespace ObenCore.Tests;

public class AssemblyRewriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "oben-rewriter-" + Guid.NewGuid().ToString("N"));

    public AssemblyRewriterTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    // A library with an enum, and an app whose method takes that enum with a default value: writing the app
    // needs the enum's underlying type, so Cecil has to find the library under its reload name.
    [Fact]
    public void AnEnumDefaultFromARenamedCopyIsResolved()
    {
        string lib = Path.Combine(_dir, "RwLib.dll");
        string app = Path.Combine(_dir, "RwApp.dll");
        var mode = WriteLibrary(lib);
        WriteApp(app, mode, _dir);
        AssemblyResolver.RegisterPath(lib);
        AssemblyResolver.RegisterPath(app);

        AssemblyRewriter.Prepare(lib, rename: true);
        var (image, _) = AssemblyRewriter.Prepare(app, rename: false);

        using var rewritten = AssemblyDefinition.ReadAssembly(new MemoryStream(image));
        Assert.Contains(rewritten.MainModule.AssemblyReferences, r => r.Name == AssemblyRewriter.CurrentName("RwLib"));
        Assert.NotEqual("RwLib", AssemblyRewriter.CurrentName("RwLib"));
        Assert.Equal("RwLib", AssemblyRewriter.OriginalName(AssemblyRewriter.CurrentName("RwLib")));
        var parameter = rewritten.MainModule.GetType("RwApp.Api").Methods.Single(m => m.Name == "Send").Parameters[0];
        Assert.Equal(7, parameter.Constant);
    }

    private static TypeDefinition WriteLibrary(string path)
    {
        var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("RwLib", new Version(1, 0)), "RwLib", ModuleKind.Dll);
        var module = assembly.MainModule;
        var mode = new TypeDefinition("RwLib", "Mode", TypeAttributes.Public | TypeAttributes.Sealed, module.ImportReference(typeof(Enum)));
        mode.Fields.Add(new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, module.TypeSystem.Int32));
        foreach (var (name, value) in new[] { ("A", 0), ("B", 7) })
            mode.Fields.Add(new FieldDefinition(name, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, mode) { Constant = value });
        module.Types.Add(mode);
        assembly.Write(path);
        return mode;
    }

    private static void WriteApp(string path, TypeDefinition mode, string libFolder)
    {
        // Writing it needs the library too, the same way the rewriter does.
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(libFolder);
        var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("RwApp", new Version(1, 0)), "RwApp",
            new ModuleParameters { Kind = ModuleKind.Dll, AssemblyResolver = resolver });
        var module = assembly.MainModule;
        var api = new TypeDefinition("RwApp", "Api", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);
        var send = new MethodDefinition("Send", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        send.Parameters.Add(new ParameterDefinition("mode", ParameterAttributes.Optional | ParameterAttributes.HasDefault, module.ImportReference(mode)) { Constant = 7 });
        send.Body.GetILProcessor().Emit(OpCodes.Ret);
        api.Methods.Add(send);
        module.Types.Add(api);
        assembly.Write(path);
    }
}
