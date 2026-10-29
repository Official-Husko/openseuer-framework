// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenFramework.Story;

/// <summary>Dealing with the police the ways the game offers: the fine, a bribe, having things confiscated.</summary>
public static partial class Law
{
    private static readonly AccessTools.FieldRef<Crime, int> LatestBribe = AccessTools.FieldRefAccess<Crime, int>("latestBribe");

    /// <summary>The police desk would take the fine right now (there is one and the player can pay it).</summary>
    public static bool CanPayFine
    {
        get
        {
            var crime = Crime.instance;
            return CanEdit(crime) && Money.instance != null && Inventory.instance != null && crime.CanPayFine();
        }
    }

    /// <summary>The player hasn't tried a bribe yet: the game allows one try until the record is cleared.</summary>
    public static bool CanBribe => Read(c => c.CanBribe(), false);

    /// <summary>
    /// What a bribe costs, in coins. The game works the price out once (from the record and the Bribery skill)
    /// and keeps it until it is paid, so asking fixes it, as the bribe dialogue does. 0 with nothing on the record.
    /// </summary>
    public static int BribePrice
    {
        get
        {
            var crime = Crime.instance;
            if (crime == null || Money.instance == null || PlayerStats.instance == null || crime.CountCrimes(false) <= 0f)
                return 0;
            crime.CalculateBribe();
            return LatestBribe(crime);
        }
    }

    /// <summary>
    /// Offers the police a bribe, as the dialogue does: one try per record, passed or failed by the Bribery
    /// skill. A passed try takes <see cref="BribePrice"/> coins and clears the record. False when refused
    /// (already tried, nothing on the record, can't pay, a save or arrest running) or when the try fails;
    /// a refusal doesn't use up the try, a failed try does.
    /// </summary>
    public static bool TryBribe()
    {
        var crime = Crime.instance;
        if (!CanEdit(crime) || Money.instance == null || PlayerStats.instance == null || !crime.CanBribe() || crime.CountCrimes(false) <= 0f)
            return false;
        try
        {
            crime.CalculateBribe();
            if (!Money.instance.CheckMoney(LatestBribe(crime)))
                return false;
            return crime.TryToBribe();
        }
        catch (Exception e)
        {
            StoryLog.Failed("Law.TryBribe", e);
            return false;
        }
    }

    /// <summary>
    /// Takes the player's things the way an arrest does: stolen items are destroyed, everything else is
    /// moved into <paramref name="into"/> (emptied first with <paramref name="clear"/>; what doesn't fit is
    /// lost). The record isn't touched.
    /// </summary>
    public static bool Confiscate(Storage into, bool clear = true)
    {
        if (into == null)
            throw new ArgumentNullException(nameof(into));
        var crime = Crime.instance;
        if (!CanEdit(crime) || Inventory.instance == null)
            return false;
        try
        {
            crime.ConfiscateItems(into, clear);
            return true;
        }
        catch (Exception e)
        {
            StoryLog.Failed("Law.Confiscate", e);
            return false;
        }
    }
}
