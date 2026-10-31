using ObenFramework.Features;
using ObenFramework.Saves;
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
        ModFeatures.Register(this, new DialogueRealMoney());
        ModFeatures.Register(this, new RandomPickSkipsLast());
        ModFeatures.Register(this, new AmbientSoundVariety());
        ModFeatures.Register(this, new DialogueLuaFunctions());
        ModFeatures.Register(this, new NpcSightChance());
        ModFeatures.Register(this, new RenovationMaterialsOnce());
        ModFeatures.Register(this, new StrayLightControllers());
        ModFeatures.Register(this, new ItemGiverRefillTime());
        ModFeatures.Register(this, new DateWeekdayShift());
        ModFeatures.Register(this, new ScreenshotFolder());
        ModFeatures.Register(this, new ScreenshotHiddenHud());
        ModFeatures.Register(this, new HeadLampPrompt());
        ModFeatures.Register(this, new AutoConsumeLastItem());
        ModFeatures.Register(this, new LanternStaleTimer());
        ModFeatures.Register(this, new HotelDayOnce());
        ModFeatures.Register(this, new CleaningDayLate());
        ModFeatures.Register(this, new GamblingNeedMood());
        BankInterest.State = ModSaves.Register<BankInterestState>(this, "bank-interest");
        ModFeatures.Register(this, new BankInterest());
    }
}
