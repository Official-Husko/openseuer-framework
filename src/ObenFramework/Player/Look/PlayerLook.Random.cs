// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Linq;
using UnityEngine;

namespace ObenFramework.Player.Look;

/// <summary>Random looks within the ranges the game's character creator allows.</summary>
public static partial class PlayerLook
{
    /// <summary>
    /// A random look: every slider within its range and a random item in each wearable slot, as rolling dice
    /// in the character creator would. Nothing is applied; pass it to <see cref="ApplyToPlayer"/>,
    /// <see cref="Apply"/> or a mod NPC. <paramref name="male"/> picks the body, random when null.
    /// </summary>
    public static LookData Random(bool? male = null)
    {
        var look = new LookData
        {
            male = male ?? UnityEngine.Random.value > 0.5f,
            advanced = UnityEngine.Random.value > 0.5f,
        };
        foreach (var dial in LookDials.All)
        {
            float value = UnityEngine.Random.Range(dial.Min, dial.Max);
            if (dial.Name == "gender")
                value = Mathf.Round(value);
            else if (dial.Name == "breastSize")
                value = look.male ? UnityEngine.Random.Range(0f, 0.3f) : UnityEngine.Random.Range(0.3f, 1f);
            dial.Set(look, value);
        }
        foreach (var slot in LookSlots.All)
        {
            var choices = WearableTable.Names(slot, look.male).ToArray();
            string pick = choices.Length > 0 ? choices[UnityEngine.Random.Range(0, choices.Length)] : "";
            slot.Set(look, pick == "-" ? "" : pick);
        }
        return look;
    }
}
