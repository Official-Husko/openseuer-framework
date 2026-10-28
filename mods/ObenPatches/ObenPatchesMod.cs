using ObenFramework.Features;
using ObenLoader;
using ObenPatches.Fixes;

namespace ObenPatches;

/// <summary>
/// Unofficial ObenPatches: one feature per game bug, each with its Harmony patches in a category
/// of the same name, so the player can switch any fix off if it gets in the way.
/// Write-ups for every bug are in docs/game/known-bugs.md.
/// </summary>
public sealed class ObenPatchesMod : ObenMod
{
    public override void OnLoad()
    {
        ModFeatures.Register(this, new MissingSavesFolder());
        ModFeatures.Register(this, new RobbersSeePolice());
        ModFeatures.Register(this, new ReflectionProbeRefresh());
        ModFeatures.Register(this, new EmailMonthNames());
    }
}
