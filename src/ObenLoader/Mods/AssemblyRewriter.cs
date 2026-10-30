using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ObenLoader.Mods;

/// <summary>
/// Gives each hot-reloaded copy of an assembly its own name ("ObenFramework-r2", "-r3", ...) and
/// points references at the newest copies. Mono binds a reference to the first loaded assembly
/// with a matching name and never lets go, so without unique names a reloaded framework would
/// keep feeding its dependents the old code.
/// </summary>
internal static class AssemblyRewriter
{
    // Original simple name -> name of the copy currently in use.
    private static readonly Dictionary<string, string> CurrentNames = new Dictionary<string, string>();
    // Every reload name -> original simple name.
    private static readonly Dictionary<string, string> OriginalNames = new Dictionary<string, string>();
    private static readonly Dictionary<string, int> Generations = new Dictionary<string, int>();

    /// <summary>True once any assembly was renamed; until then nothing needs rewriting.</summary>
    public static bool Active => CurrentNames.Count > 0;

    public static string CurrentName(string original) =>
        CurrentNames.TryGetValue(original, out var name) ? name : original;

    public static string OriginalName(string name) =>
        OriginalNames.TryGetValue(name, out var original) ? original : name;

    /// <summary>
    /// Returns (image, symbols) ready for Assembly.Load. <paramref name="rename"/> gives the assembly
    /// a fresh name; references to renamed assemblies are always redirected.
    /// </summary>
    public static (byte[] Image, byte[] Symbols) Prepare(string path, bool rename)
    {
        string pdbPath = Path.ChangeExtension(path, ".pdb");
        byte[] symbols = File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : null;
        return Prepare(File.ReadAllBytes(path), symbols, rename);
    }

    /// <summary>Same as <see cref="Prepare(string, bool)"/> for an image that's already in memory.</summary>
    public static (byte[] Image, byte[] Symbols) Prepare(byte[] image, byte[] symbols, bool rename)
    {
        bool hasSymbols = symbols != null;

        using var resolver = new RewriteResolver(OriginalName);
        using var input = new MemoryStream(image);
        using var pdbInput = hasSymbols ? new MemoryStream(symbols) : null;
        var readParams = new ReaderParameters
        {
            ReadSymbols = hasSymbols,
            SymbolStream = pdbInput,
            SymbolReaderProvider = hasSymbols ? new PortablePdbReaderProvider() : null,
            ReadingMode = ReadingMode.Immediate,
            AssemblyResolver = resolver,
        };

        AssemblyDefinition assembly;
        try
        {
            assembly = AssemblyDefinition.ReadAssembly(input, readParams);
        }
        catch (System.Exception e) when (hasSymbols && (e is SymbolsNotMatchingException || e is System.BadImageFormatException))
        {
            // A missing or mismatched PDB only costs line numbers in stack traces.
            input.Position = 0;
            hasSymbols = false;
            assembly = AssemblyDefinition.ReadAssembly(input, new ReaderParameters { ReadingMode = ReadingMode.Immediate, AssemblyResolver = resolver });
        }

        using (assembly)
        {
            string original = assembly.Name.Name;
            if (rename)
            {
                int generation = Generations.TryGetValue(original, out int g) ? g + 1 : 2;
                Generations[original] = generation;
                assembly.Name.Name = original + "-r" + generation;
                CurrentNames[original] = assembly.Name.Name;
                OriginalNames[assembly.Name.Name] = original;

                // A new identity needs a new module id too. Harmony (through MonoMod) maps modules by
                // this id, and with the old one it resolves patch methods in the old copy.
                assembly.MainModule.Mvid = System.Guid.NewGuid();
            }

            foreach (var reference in assembly.MainModule.AssemblyReferences)
            {
                if (CurrentNames.TryGetValue(reference.Name, out string current))
                    reference.Name = current;
            }

            using var output = new MemoryStream();
            using var pdbOutput = hasSymbols ? new MemoryStream() : null;
            var writeParams = hasSymbols
                ? new WriterParameters { WriteSymbols = true, SymbolStream = pdbOutput, SymbolWriterProvider = new PortablePdbWriterProvider() }
                : new WriterParameters();
            assembly.Write(output, writeParams);
            return (output.ToArray(), pdbOutput?.ToArray());
        }
    }
}
