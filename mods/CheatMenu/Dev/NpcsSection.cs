// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Npcs;
using ObenFramework.Player;
using ObenUIKit;
using ObenUIKit.Controls;
using UnityEngine;

namespace CheatMenu.Dev;

/// <summary>
/// NPC tools: the NPCs in this scene nearest first, and for the one picked its state and activity, going to
/// it, bringing it over, making it follow or look, stopping what it does, killing it. Below come the mod
/// NPCs (<see cref="ModNpcTools"/>) and the relationships (<see cref="RelationsSection"/>). Everything goes
/// through the framework's <see cref="Npcs"/> and <see cref="NpcActions"/>.
/// </summary>
internal static class NpcsSection
{
    private const int Rows = 8;
    private const float Near = 1.5f;

    private static readonly List<NpcRecord> Here = new List<NpcRecord>();
    private static int _picked = -1;

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        if (!WorldState.IsReady || !PlayerRef.Position.HasValue)
        {
            ui.Header("NPCs");
            ui.Label(CheatContext.NeedsGame, muted: true);
            return;
        }
        BuildHere(ui, ctx);
        ModNpcTools.Build(ui, ctx);
        RelationsSection.Build(ui, ctx);
    }

    private static void BuildHere(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("NPCs in this scene");
        var me = PlayerRef.Position.Value;
        Here.Clear();
        Here.AddRange(Npcs.InScene());
        // The ones up and about first, then by distance.
        Here.Sort((a, b) => Away(a) != Away(b) ? Away(a).CompareTo(Away(b))
            : (a.Position - me).sqrMagnitude.CompareTo((b.Position - me).sqrMagnitude));
        if (Here.Count == 0)
        {
            ui.Label("No NPC is in this scene.", muted: true);
            return;
        }

        ui.Label((Here.Count == 1 ? "1 NPC" : $"{Here.Count} NPCs, nearest first") + "; the distances are from when the list was made.", muted: true);
        ui.List(Here.Count, (i, row) =>
        {
            var npc = Here[i];
            row.Text = UiText.Plain($"{npc.FullName} ({npc.Id})");
            row.Detail = UiText.Plain($"{Vector3.Distance(npc.Position, me):0} m" + (npc.Dead ? ", dead" : npc.Active ? "" : ", hidden"));
            row.Muted = npc.Dead || !npc.Active;
        }, i =>
        {
            _picked = Here[i].Id;
            ui.Window.Defer(ui.Window.Rebuild);
        }, Mathf.Clamp(Here.Count, 2, Rows));
        ui.Button("Make the list again", () => ui.Window.Defer(ui.Window.Rebuild));

        var picked = Here.Find(n => n.Id == _picked);
        if (picked == null)
        {
            ui.Label("Pick an NPC in the list for its tools.", muted: true);
            return;
        }
        BuildPicked(ui, ctx, picked.Id);
    }

    private static void BuildPicked(UiBuilder ui, CheatContext ctx, int id)
    {
        ui.Header(UiText.Plain(Npcs.Get(id)?.FullName ?? id.ToString()));
        ui.Live(() => Describe(Npcs.Get(id)), muted: true);
        ui.Row(r =>
        {
            r.Button("Go to it", () => ctx.Run("Go to the NPC", () => GoTo(ctx, id)));
            r.Button("Bring it here", () => DevActions.Act(ctx, ui, "Bring the NPC here",
                () => PlayerRef.Position.HasValue && NpcActions.Teleport(id, InFront(2f)),
                "it has no path finding (most of the game's NPCs stand still), or it isn't here."));
            r.Button("Look at me", () => DevActions.Act(ctx, ui, "Look at me",
                () => NpcActions.LookAtPlayer(id, 5), "it can't turn its head."));
        });
        ui.Row(r =>
        {
            r.Button("Follow me", () => DevActions.Act(ctx, ui, "Follow me",
                () => NpcActions.FollowPlayer(id), "it can't follow (no path finding, or busy with a scene)."));
            r.Button("Stop following", () => DevActions.Act(ctx, ui, "Stop following",
                () => NpcActions.StopFollowing(id), "it wasn't following."));
        });
        ui.Row(r =>
        {
            r.Button("Stop its activity", () => DevActions.Act(ctx, ui, "Stop its activity",
                () => NpcActions.StopActivity(id), "it isn't doing anything."));
            r.Button("Kill it", () => UiKit.Confirm(DevWindow.Owner ?? (ObenLoader.Runtime.ITracker)ctx.Mod, "Kill this NPC?",
                "It dies the way it does in the game, and the save keeps it dead.",
                () => DevActions.Act(ctx, ui, "Kill the NPC", () => NpcActions.Kill(id), "it can't be killed here.")));
        });
    }

    private static bool Away(NpcRecord npc) => npc.Dead || !npc.Active;

    private static string Describe(NpcRecord npc)
    {
        if (npc == null || !npc.Loaded)
            return "It isn't in this scene any more.";
        var me = PlayerRef.Position;
        string where = me.HasValue ? $"{Vector3.Distance(npc.Position, me.Value):0.0} m away" : "";
        string job = npc.Job.Length > 0 ? npc.Job + ". " : "";
        string activity = npc.Activity ?? "nothing";
        string entry = npc.ScheduleEntry != null ? $" (timetable: {npc.ScheduleEntry})" : "";
        return UiText.Plain($"{job}State {npc.State}, doing {activity}{entry}. {where}");
    }

    // Puts the player a step in front of the NPC, facing it: in front is where its room is, while the side
    // the player came from can be the other side of a wall.
    private static void GoTo(CheatContext ctx, int id)
    {
        var npc = Npcs.Get(id);
        var me = PlayerRef.Position;
        if (npc == null || !npc.Loaded || !me.HasValue)
        {
            ctx.Say("It isn't in this scene any more.");
            return;
        }
        var away = npc.Live != null ? npc.Live.transform.forward : me.Value - npc.Position;
        away.y = 0f;
        var side = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
        var spot = npc.Position + side * Near;
        if (PlayerTeleport.TryFindGround(spot, out var ground, 5f))
            spot = ground;
        float yaw = Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg;
        PlayerTeleport.To(spot, yaw);
        ctx.Say("Next to " + npc.FullName + ".");
    }

    /// <summary>A point <paramref name="distance"/> metres in front of the player, on the ground when there is some.</summary>
    internal static Vector3 InFront(float distance)
    {
        var me = PlayerRef.Position.Value;
        float yaw = PlayerRef.LookYaw ?? PlayerRef.Yaw ?? 0f;
        var spot = me + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * distance;
        return PlayerTeleport.TryFindGround(spot, out var ground, 5f) ? ground : spot;
    }
}
