// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>
/// Watches on live values: type a type, pick one of its members (or type a longer path), and the value is
/// shown and read again four times a second while the window is open. The player's watches are kept in the
/// mod's config.json; watches mods add through <see cref="DevMenu.AddWatch"/> are listed below them.
/// Reading is plain reflection on the main thread, so it only costs anything while the section is shown.
/// </summary>
internal static class WatchesSection
{
    public const int MaxWatches = 20;

    private static string _type = "TimeOfDayAzure";
    private static string _path = "timeSpeedOverrideFactor";
    private static List<string> _members = new List<string>();

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Watches");
        ui.Label("A type, then a static field or property, or one of a game singleton's (types with a static 'instance'). Longer paths like currentTimeAndDay.hours work too.", muted: true);
        ui.TextField("Type (Enter lists its members)", _type, t => _type = t, t =>
        {
            _type = t;
            _members = LiveWatch.MemberNames(t);
            ctx.Say(_members.Count == 0 ? $"No type named '{t}', or it has nothing to watch." : $"{_members.Count} members of {t}.");
            ui.Window.Rebuild();
        });
        FieldHandle path = null;
        if (_members.Count > 0)
        {
            int index = System.Math.Max(0, _members.IndexOf(_path));
            ui.Cycle("Member", _members.ToArray(), index, i =>
            {
                _path = _members[i];
                path?.Set(_path);
            });
        }
        path = ui.TextField("Member or path", _path, t => _path = t);
        ui.Button("Watch it", () => Add(ctx, ui));

        BuildList(ui, ctx);
        BuildModWatches(ui);
    }

    private static void Add(CheatContext ctx, UiBuilder ui)
    {
        var watches = ctx.Config.Value.Watches;
        if (watches.Count >= MaxWatches)
        {
            ctx.Say($"At most {MaxWatches} watches; remove one first.");
            return;
        }
        var watch = LiveWatch.Create(_type, _path, out string error);
        if (watch == null)
        {
            ctx.Say(error);
            return;
        }
        if (!watches.Contains(watch.Key))
        {
            watches.Add(watch.Key);
            ctx.Config.Save();
        }
        ctx.Say("Watching " + watch.Key + ".");
        ui.Window.Rebuild();
    }

    private static void BuildList(UiBuilder ui, CheatContext ctx)
    {
        var watches = ctx.Config.Value.Watches;
        if (watches.Count == 0)
            return;
        ui.Header("Your watches");
        foreach (string key in watches.ToArray())
        {
            string kept = key;
            LiveWatch watch = null;
            string error = "not a Type:path entry";
            if (DevRules.TrySplitWatch(kept, out string type, out string path))
                watch = LiveWatch.Create(type, path, out error);
            if (watch != null)
                ui.Live(() => kept + " = " + watch.Read());
            else
                ui.Label(kept + ": " + error, muted: true);
            ui.Button("Stop watching " + kept, () =>
            {
                watches.Remove(kept);
                ctx.Config.Save();
                ui.Window.Rebuild();
            });
        }
    }

    private static void BuildModWatches(UiBuilder ui)
    {
        if (DevMenu.Watches.Count == 0)
            return;
        ui.Header("From mods");
        foreach (var watch in DevMenu.Watches)
        {
            var entry = watch;
            ui.Live(() => $"{entry.ModId} {entry.Name} = {Read(entry)}");
        }
    }

    private static string Read(DevWatch watch)
    {
        try
        {
            return DevRules.Cut(watch.Read() ?? "null", 160);
        }
        catch (System.Exception e)
        {
            return DevRules.Cut(e.GetType().Name + ": " + e.Message, 160);
        }
    }
}
