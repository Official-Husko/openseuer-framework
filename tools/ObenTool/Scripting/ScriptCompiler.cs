using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using ObenTool.Game;

namespace ObenTool.Scripting;

/// <summary>Compiles a script into a DLL (plus portable PDB) the game can load from memory.</summary>
public static class ScriptCompiler
{
    public sealed record Output(byte[] Image, byte[] Symbols);

    public static Output Compile(GameInstall game, string code, string fileLabel)
    {
        string source = ScriptSource.Prepare(code, fileLabel);
        var tree = CSharpSyntaxTree.ParseText(source,
            new CSharpParseOptions(LanguageVersion.Latest), path: fileLabel, encoding: Encoding.UTF8);

        // Mono never loads the same assembly name twice, so every compile gets a fresh one.
        string name = "ObenScript-" + Guid.NewGuid().ToString("N")[..12];
        var compilation = CSharpCompilation.Create(name, new[] { tree }, ScriptReferences.For(game),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: true));

        using var image = new MemoryStream();
        using var symbols = new MemoryStream();
        EmitResult result = compilation.Emit(image, symbols,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(Describe);
            throw new ArgumentException("The script doesn't compile:\n" + string.Join("\n", errors));
        }
        return new Output(image.ToArray(), symbols.ToArray());
    }

    private static string Describe(Diagnostic d)
    {
        var span = d.Location.GetMappedLineSpan();
        return span.IsValid
            ? $"  {span.Path}({span.StartLinePosition.Line + 1},{span.StartLinePosition.Character + 1}): {d.GetMessage()}"
            : "  " + d.GetMessage();
    }
}
