// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Npcs;
using ObenFramework.Player;
using ObenUIKit.Controls;
using UnityEngine;

namespace CheatMenu.Dev;

/// <summary>
/// The mod NPCs in this scene, whichever mod made them: go to one, bring it over, despawn it (which also
/// forgets a saved one). And a copy of any game character spawned in front of the player, for trying looks,
/// orders or dialogue; copies made here aren't saved and go at the next level change.
/// </summary>
internal static class ModNpcTools
{
    private const int MaxShown = 12;
    private const string NamePrefix = "dev";

    private static string _template = "";
    private static string _firstName = "Copy";

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Mod NPCs");
        var all = ModNpcs.All();
        ui.Label(all.Count == 0 ? "No mod NPC is in this scene." : all.Count == 1 ? "1 mod NPC in this scene." : $"{all.Count} mod NPCs in this scene.", muted: true);
        for (int i = 0; i < all.Count && i < MaxShown; i++)
        {
            var npc = all[i];
            ui.Label(UiText.Plain($"{npc.FullName} ({npc.Owner}/{npc.Name}, {npc.CharacterId})" + (npc.Saved ? ", saved" : "")));
            ui.Row(r =>
            {
                r.Button("Go to it", () => ctx.Run("Go to the NPC", () =>
                {
                    PlayerTeleport.To(npc.Position + Vector3.back * 1.5f, 0f);
                    ctx.Say("Next to " + npc.FullName + ".");
                }));
                r.Button("Bring it here", () => DevActions.Act(ctx, ui, "Bring the NPC here",
                    () => npc.Teleport(NpcsSection.InFront(2f)), "it isn't loaded."));
                r.Button("Despawn", () => DevActions.Act(ctx, ui, "Despawn " + npc.FullName,
                    () => npc.Despawn(), "refused while the game saves or loads."));
            });
        }
        if (all.Count > MaxShown)
            ui.Label($"And {all.Count - MaxShown} more.", muted: true);

        ui.Header("Spawn a copy");
        ui.Label("A copy of any game character (its body, voice and look) in front of you. npcs.list over the bridge lists the character ids. Copies made here aren't kept in the save.", muted: true);
        ui.TextField("Character id to copy", _template, t => _template = t);
        ui.TextField("First name", _firstName, t => _firstName = t);
        ui.Button("Spawn it", () => Spawn(ctx, ui));
    }

    private static void Spawn(CheatContext ctx, UiBuilder ui)
    {
        if (!int.TryParse((_template ?? "").Trim(), out int template) || template <= 0)
        {
            ctx.Say("Type a character id first, a whole number above 0.");
            return;
        }
        string first = string.IsNullOrWhiteSpace(_firstName) ? "Copy" : _firstName.Trim();
        string name = FreeName(ctx);
        var spec = new ModNpcSpec
        {
            Template = template,
            FirstName = first,
            Position = NpcsSection.InFront(2f),
            // Facing the player.
            Yaw = (PlayerRef.LookYaw ?? PlayerRef.Yaw ?? 0f) + 180f,
        };
        DevActions.Act(ctx, ui, $"Spawn a copy of {template}", () => ModNpcs.Spawn(ctx.Mod, name, spec) != null,
            "it couldn't be made (the framework log says why: an unknown character, no ground, or a save running).");
    }

    // dev1, dev2, ...: the first name the Cheat Menu isn't using for an NPC here.
    private static string FreeName(CheatContext ctx)
    {
        for (int i = 1; ; i++)
        {
            string name = NamePrefix + i;
            if (ModNpcs.Get(ctx.Mod, name) == null)
                return name;
        }
    }
}
