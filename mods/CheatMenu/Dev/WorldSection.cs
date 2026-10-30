// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Player;
using ObenFramework.Transport;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>
/// Player and world tools beyond the Cheats window: set any player stat by its console name or any skill, go
/// to an entrypoint of the level or to coordinates, and the item tools (<see cref="ItemTools"/>). Through the
/// framework's <see cref="PlayerStat"/>, <see cref="PlayerSkills"/>, <see cref="Travel"/> and <see cref="PlayerTeleport"/>.
/// </summary>
internal static class WorldSection
{
    private static int _stat;
    private static int _statShown = -1;
    private static float _statValue;
    private static int _skill;
    private static int _skillShown = -1;
    private static float _skillValue;
    private static int _entry;
    private static string _x = "", _y = "", _z = "";

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        if (!WorldState.IsReady)
        {
            ui.Header("Player and world");
            ui.Label(CheatContext.NeedsGame, muted: true);
            return;
        }
        BuildStats(ui, ctx);
        BuildSkills(ui, ctx);
        BuildEntrypoints(ui, ctx);
        BuildCoordinates(ui, ctx);
        ItemTools.Build(ui, ctx);
    }

    private static void BuildStats(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Player stats");
        var stats = PlayerStat.All;
        if (stats.Count == 0)
            return;
        if (_stat >= stats.Count)
            _stat = 0;
        var names = new string[stats.Count];
        for (int i = 0; i < stats.Count; i++)
            names[i] = stats[i].Name;

        var stat = stats[_stat];
        if (_statShown != _stat)
        {
            // The slider starts at the stat's current value whenever another stat is picked.
            _statShown = _stat;
            _statValue = stat.TryGet(out float now) ? now : 0f;
        }
        ui.Cycle("Stat", names, _stat, i =>
        {
            _stat = i;
            ui.Window.Rebuild();
        });
        ui.Live(() => stat.TryGet(out float value) ? $"{stat.Description} Now {value:0.##}." : "Not available here.", muted: true);
        float min = stat.Min ?? -100f;
        float max = stat.Max ?? System.Math.Max(100f, min + 100f);
        ui.Slider("Value", min, max, UnityEngine.Mathf.Clamp(_statValue, min, max), v => _statValue = v, "0.#");
        ui.Button("Set the stat", () => DevActions.Change(ctx, ui, $"{stat.Name} to {_statValue:0.#}", () => stat.Set(_statValue)));
    }

    private static void BuildSkills(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Skills");
        var skills = PlayerSkills.All();
        if (skills.Count == 0)
            return;
        if (_skill >= skills.Count)
            _skill = 0;
        var names = new string[skills.Count];
        for (int i = 0; i < skills.Count; i++)
            names[i] = skills[i].Skill.ToString();

        var skill = skills[_skill].Skill;
        if (_skillShown != _skill)
        {
            _skillShown = _skill;
            _skillValue = skills[_skill].Value;
        }
        ui.Cycle("Skill", names, _skill, i =>
        {
            _skill = i;
            ui.Window.Rebuild();
        });
        ui.Live(() => $"Now {PlayerSkills.Get(skill):0.##}; the whole part is the level, from 0 to {PlayerSkills.MaxLevel}.", muted: true);
        ui.Slider("Value", 0f, PlayerSkills.MaxLevel, UnityEngine.Mathf.Clamp(_skillValue, 0f, PlayerSkills.MaxLevel), v => _skillValue = v, "0.##");
        ui.Button("Set the skill", () => DevActions.Change(ctx, ui, $"{skill} to {_skillValue:0.##}", () => PlayerSkills.Set(skill, _skillValue)));
    }

    // X, Y and Z start at the player's position the first time, and "Where I am" fills them again.
    private static void BuildCoordinates(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Coordinates");
        if (_x.Length == 0 && PlayerRef.Position.HasValue)
            Fill();
        ui.Live(() => PlayerRef.Position.HasValue ? $"You are at {Text(PlayerRef.Position.Value)}." : "", muted: true);
        ui.TextField("X", _x, t => _x = t);
        ui.TextField("Y (up)", _y, t => _y = t);
        ui.TextField("Z", _z, t => _z = t);
        ui.Row(r =>
        {
            r.Button("Where I am", () =>
            {
                Fill();
                ui.Window.Defer(ui.Window.Rebuild);
            });
            r.Button("Go to these coordinates", () => ctx.Run("Teleport", () =>
            {
                if (!DevRules.TryParseVector(_x, _y, _z, out float x, out float y, out float z))
                {
                    ctx.Say("X, Y and Z take numbers, such as 12.5.");
                    return;
                }
                PlayerTeleport.To(new UnityEngine.Vector3(x, y, z));
                ctx.Say($"At {x:0.##}, {y:0.##}, {z:0.##}.");
            }));
        });
    }

    private static void Fill()
    {
        var at = PlayerRef.Position ?? UnityEngine.Vector3.zero;
        _x = Number(at.x);
        _y = Number(at.y);
        _z = Number(at.z);
    }

    private static string Number(float value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static string Text(UnityEngine.Vector3 at) => $"{Number(at.x)}, {Number(at.y)}, {Number(at.z)}";

    private static void BuildEntrypoints(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Entrypoints of this level");
        // A level can list the same entrypoint more than once.
        var points = new List<string>();
        foreach (string name in Travel.Entrypoints())
        {
            if (!points.Contains(name))
                points.Add(name);
        }
        if (points.Count == 0)
        {
            ui.Label("This level has no entrypoints.", muted: true);
            return;
        }
        if (_entry >= points.Count)
            _entry = 0;
        ui.Cycle("Entrypoint", points.ToArray(), _entry, i => _entry = i);
        ui.Button("Go to the entrypoint", () => ctx.Run("Teleport", () =>
        {
            string name = points[_entry];
            ctx.Say(Travel.TeleportTo(name) ? $"At {name}." : $"Couldn't go to {name}.");
        }));
    }
}
