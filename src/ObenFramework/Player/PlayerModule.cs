using ObenLoader;

namespace ObenFramework.Player;

/// <summary>
/// Sets up the parts of the player module that keep state: movement multipliers, sleep and waits,
/// skills, effects and the rest. Their Harmony patches are applied by the loader; everything set up
/// here is undone when the framework unloads.
/// </summary>
internal static class PlayerModule
{
    public static void Init(ObenMod framework)
    {
        PlayerMovement.Init(framework);
        ModWait.Init(framework);
        TimePicker.Init(framework);
        PlayerSkills.Init(framework);
        CustomSkills.Init(framework);
        SkillBanners.Init(framework);
        CustomSkillRows.Init(framework);
        PlayerEffects.Init(framework);
        CustomInteractions.Init(framework);
        Weapons.Init(framework);
    }
}
