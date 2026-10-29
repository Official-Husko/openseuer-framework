using ObenLoader;
using ObenLoader.Mods;

namespace ObenCore.Tests;

// A mod declared in code, read back from this test assembly.
[ObenModInfo("CodeMod", "Code Mod", "1.4.2", Author = "Someone", GameVersion = "0.5.x", HotReload = true, AnyGameVersion = true)]
[ObenModDependency("ObenFramework", "0.1.0")]
[ObenModDependency("ObenUIKit")]
internal sealed class DeclaredInCode
{
}

public class CodeManifestTests
{
    private static ModManifest Read() =>
        CodeManifest.ReadAssembly(typeof(DeclaredInCode).Assembly.Location, null)!;

    [Fact]
    public void ReadsTheDeclaration()
    {
        var manifest = Read();
        Assert.Equal("CodeMod", manifest.Id);
        Assert.Equal("Code Mod", manifest.Name);
        Assert.Equal("1.4.2", manifest.Version);
        Assert.Equal("Someone", manifest.Author);
        Assert.Equal("0.5.x", manifest.GameVersion);
        Assert.True(manifest.HotReload);
    }

    [Fact]
    public void ReadsTheDependencies()
    {
        var manifest = Read();
        Assert.Equal("0.1.0", manifest.Dependencies["ObenFramework"]);
        Assert.Equal("*", manifest.Dependencies["ObenUIKit"]);
    }

    [Fact]
    public void OnlyItsOwnDllIsTheMod()
    {
        var manifest = Read();
        Assert.Equal(new[] { Path.GetFileName(typeof(DeclaredInCode).Assembly.Location) }, manifest.Assemblies);
    }

    [Fact]
    public void ALooseDllIsListedByItsFullPath()
    {
        string path = typeof(DeclaredInCode).Assembly.Location;
        var manifest = CodeManifest.ReadLoose(path, null)!;
        Assert.Equal("CodeMod", manifest.Id);
        Assert.Equal(new[] { Path.GetFullPath(path) }, manifest.Assemblies);
    }

    [Fact]
    public void ALooseLibraryIsNoMod() =>
        Assert.Null(CodeManifest.ReadLoose(typeof(Xunit.FactAttribute).Assembly.Location, null));

    [Fact]
    public void TheAnyGameVersionFlagIsRead() => Assert.True(Read().AnyGameVersion);

    [Fact]
    public void ADllWithoutTheAttributeIsNoMod() =>
        Assert.Null(CodeManifest.ReadAssembly(typeof(Xunit.FactAttribute).Assembly.Location, null));

    [Fact]
    public void ANonAssemblyIsSkippedWithAWarning()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not a dll");
            string? warning = null;
            Assert.Null(CodeManifest.ReadAssembly(path, w => warning = w));
            Assert.NotNull(warning);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
