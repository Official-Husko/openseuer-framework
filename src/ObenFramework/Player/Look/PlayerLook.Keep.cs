using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player.Look;

public static partial class PlayerLook
{
    // Looks mods asked to keep, newest last; the newest one is worn.
    private static readonly List<KeptEntry> Kept = new List<KeptEntry>();

    // What the player's body wore before a kept look replaced it, to put back when the last one goes.
    private static Appearance _originalFor;
    private static Character _original;

    /// <summary>The look mods keep on the player (the newest one), or null when none does.</summary>
    public static LookData KeptLook => Kept.Count > 0 ? Kept[Kept.Count - 1].Look.Clone() : null;

    /// <summary>
    /// Dresses the player in <paramref name="look"/> and keeps them in it across level loads (the game
    /// rebuilds the player's body on each one and would otherwise bring back the player's own look). If
    /// several mods keep a look, the newest wins and the next one returns when it goes. Dispose the result
    /// or unload the mod to let go; the player's own look comes back right away. Works at the main menu
    /// too: the look is put on once a level loads. The game doesn't save the look, and neither does this.
    /// </summary>
    public static IDisposable Keep(ObenMod owner, LookData look)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (look == null)
            throw new ArgumentNullException(nameof(look));

        var entry = new KeptEntry(owner.Info.Id, look.Clone());
        Kept.Add(entry);
        WearKept();
        return owner.Track(Disposable.Create(() => Release(entry)));
    }

    internal static void InitKeep(ObenMod framework)
    {
        // Before the body's Start where possible, so the new level never shows the player's own look.
        GameEvents.SceneLoaded.Subscribe(framework, _ =>
        {
            PlayerAppearances.Forget();
            WearKept();
        });
        // The level may build the body later than the scene load; this catches that.
        GameEvents.WorldReady.Subscribe(framework, _ => WearKept());
        framework.Track(Disposable.Create(() =>
        {
            bool wearing = Kept.Count > 0;
            Kept.Clear();
            if (wearing)
                PutBackOriginal();
            _originalFor = null;
            _original = null;
        }));
    }

    private static void Release(KeptEntry entry)
    {
        int index = Kept.IndexOf(entry);
        if (index < 0)
            return;
        bool wasWorn = index == Kept.Count - 1;
        Kept.RemoveAt(index);
        if (!wasWorn)
            return;
        if (Kept.Count > 0)
            WearKept();
        else
            PutBackOriginal();
    }

    private static void WearKept()
    {
        if (Kept.Count == 0)
            return;
        var appearance = PlayerAppearance;
        if (appearance == null)
            return;

        var top = Kept[Kept.Count - 1];
        if (top.Costume != null && appearance.createdCharacter == top.Costume)
            return; // Already wearing it.

        if (appearance != _originalFor)
        {
            _originalFor = appearance;
            var current = appearance.createdCharacter;
            _original = current == null ? WhatStartLoads(appearance) : Costumes.Contains(current) ? null : current;
        }
        if (Apply(appearance, top.Look))
            top.Costume = appearance.createdCharacter;
    }

    private static void PutBackOriginal()
    {
        var appearance = _originalFor;
        var original = _original;
        _originalFor = null;
        _original = null;
        if (appearance == null || original == null)
            return;
        try
        {
            Release(appearance);
            appearance.createdCharacter = original;
            if (PlayerAppearances.IsStarted(appearance))
                appearance.LoadCharacter();
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Putting the player's own look back failed", e);
        }
    }

    // A body that hasn't started has no Character yet; Appearance.Start would load this one.
    private static Character WhatStartLoads(Appearance appearance)
    {
        var parent = appearance.transform.parent;
        var grandparent = parent != null ? parent.parent : null;
        if (grandparent == null)
            return null;
        var own = UnityEngine.Resources.Load<Character>("Characters/" + grandparent.name);
        return own != null ? own : UnityEngine.Resources.Load<Character>("Characters/Random NPC");
    }

    private sealed class KeptEntry
    {
        public KeptEntry(string ownerId, LookData look)
        {
            OwnerId = ownerId;
            Look = look;
        }

        public string OwnerId { get; }
        public LookData Look { get; }

        /// <summary>The private Character made for the current body, to tell whether it is still worn.</summary>
        public Character Costume { get; set; }
    }
}
