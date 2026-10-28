using ObenLoader.Logging;

namespace ObenPatches.Fixes;

/// <summary>Logger for patches, which are static classes and have no mod instance to log through.</summary>
internal static class PatchLog
{
    public static readonly ModLogger Log = new ModLogger("ObenPatches");
}
