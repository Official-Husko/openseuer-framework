// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Animals;

/// <summary>
/// The player's Osrat, the handheld signal finder that beeps and lights up near hidden signal sources: what
/// it reads right now. Zero and false outside a level.
/// </summary>
public static class Osrats
{
    /// <summary>Signal strength the device shows, as the game's whole-number steps.</summary>
    public static int Strength => OsratManager.instance != null ? OsratManager.instance.GetStrength() : 0;

    /// <summary>The raw signal strength from the sources in range.</summary>
    public static float Signal => OsratManager.instance != null ? OsratManager.instance.GetSignalStrenght() : 0f;

    /// <summary>The device is beeping.</summary>
    public static bool IsBeeping => OsratManager.instance != null && OsratManager.instance.beepOn;

    /// <summary>The player holds the device out.</summary>
    public static bool IsEquipped => OsratManager.instance != null && OsratManager.instance.osratVisible;

    /// <summary>Something disturbs the signal.</summary>
    public static bool IsDisturbed => OsratManager.instance != null && OsratManager.instance.disturbedSignal;
}
