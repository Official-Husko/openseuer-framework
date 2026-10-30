// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Console;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine.SceneManagement;

namespace ObenFramework.SceneLook;

/// <summary>
/// Sets up scene looks and the read-only <c>scenelooks</c> command of the F1 console, which lists the
/// looks of a scene (no scene has to be loaded) and finds them by name or character.
/// </summary>
internal static class SceneLookModule
{
    private const string Usage = "scenelooks list|listall <scene> | find \"<scene>\" <name> | findchar <id> | here | checks | status";

    public static void Init(ObenMod framework)
    {
        GameConsole.Register(framework, "scenelooks", Usage, Run);
        framework.Track(Disposable.Create(SceneLooks.Shutdown));
    }

    internal static string Run(string[] args)
    {
        try { return Text(args); }
        catch (Exception e) { return "scenelooks failed: " + e.Message; }
    }

    private static string Text(string[] args)
    {
        if (args == null || args.Length == 0)
            return Usage;
        string mode = args[0];
        if (mode == "status")
            return SceneLooks.Unavailable ?? SceneLooks.Status();
        if (mode == "list" && args.Length >= 2)
            return ListText(Join(args, 1), true);
        if (mode == "listall" && args.Length >= 2)
            return ListText(Join(args, 1), false);
        if (mode == "here")
            return ListText(SceneManager.GetActiveScene().name, true);
        if (mode == "checks")
        {
            string summary = Checks(out string full);
            return full ?? summary;
        }
        if (mode == "find" && args.Length >= 3)
            return One(SceneLooks.FindByName(args[1], Join(args, 2)), "no look named " + Join(args, 2) + " in " + args[1]);
        if (mode == "findchar" && args.Length >= 2)
        {
            if (!int.TryParse(args[1], out int id))
                return "the character id must be a number";
            return One(SceneLooks.FindByCharacter(id), "no look linked to character " + id);
        }
        return Usage;
    }

    /// <summary>
    /// The looks of the active scene, characters and then every body, with the NPC and character names among them
    /// in <paramref name="full"/>; returns a one-line summary, or why it can't run (then <paramref name="full"/> is null).
    /// </summary>
    internal static string Checks(out string full)
    {
        full = null;
        string problem = SceneLooks.Unavailable;
        if (problem != null)
            return problem;
        string scene = SceneManager.GetActiveScene().name;
        if (string.IsNullOrEmpty(scene))
            return "there is no active scene";
        var characters = SceneLooks.List(scene, true);
        var roots = SceneLooks.List(scene, false);
        var npcs = new List<string>();
        var named = new List<string>();
        foreach (var look in roots)
        {
            if (look.IsNpc)
                npcs.Add(look.Name);
            if (look.HasCharacter)
                named.Add(look.CharacterId + " " + look.CharacterName);
        }
        full = ListText(scene, true, characters) + "\n\n" + ListText(scene, false, roots) +
            "\n\nnpcs: " + Listed(npcs) + "\ncharacters: " + Listed(named);
        return scene + ": " + characters.Length + " characters, " + roots.Length + " bodies";
    }

    private static string Listed(List<string> names) => names.Count == 0 ? "none" : string.Join(", ", names.ToArray());

    private static string ListText(string scene, bool charactersOnly) => ListText(scene, charactersOnly, SceneLooks.List(scene, charactersOnly));

    private static string ListText(string scene, bool charactersOnly, SceneLookInfo[] looks)
    {
        var lines = new List<string> { "scene=" + scene + " mode=" + (charactersOnly ? "characters only" : "all bodies") };
        foreach (var look in looks)
            lines.Add(Line(look));
        lines.Add("looks: " + looks.Length);
        if (looks.Length == 0)
            lines.Add("no entries, see the framework log for the reason");
        return string.Join("\n", lines.ToArray());
    }

    private static string One(SceneLookInfo look, string missing) => look == null ? missing : Line(look);

    internal static string Line(SceneLookInfo look) =>
        $"name={look.Name} path={look.Path} renderers={look.Renderers} bones={look.Bones} rig={look.RigKind} avatar={look.AvatarKind} " +
        $"animator={(look.HasAnimator ? "yes" : "no")} npc={(look.IsNpc ? "yes" : "no")} clean={(look.Clean ? "yes" : "no")} " +
        $"character={(look.HasCharacter ? look.CharacterId + " " + look.CharacterName : "-")} scene={look.Scene} level={look.Level}";

    // Scene names have spaces; the console splits on them, so the rest of the line is the name.
    private static string Join(string[] args, int from) => string.Join(" ", args, from, args.Length - from);
}
