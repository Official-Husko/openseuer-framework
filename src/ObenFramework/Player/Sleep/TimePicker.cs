// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player;

/// <summary>
/// Asks the player for a length of time with the game's own sleep clock: the same slider, arrow keys and
/// buttons as a bed, with the mod's title and confirm label. Nothing happens to the player; the mod gets
/// the minutes and decides (start a <see cref="PlayerSleep.Wait(ObenMod, int, WaitKind, WaitOptions, Action{WaitResult})"/>,
/// book a job, set a timer).
/// </summary>
public static class TimePicker
{
    private static TimePickerSession _open;

    /// <summary>A picker is on screen.</summary>
    public static bool IsOpen => _open != null;

    /// <summary>
    /// Opens the picker. False when it can't open now: no player, the player is resting, the sleep clock is
    /// already up, or another picker is open. Exactly one of <paramref name="onConfirm"/> (with the minutes)
    /// and <paramref name="onCancel"/> is called later, unless the owner unloads first.
    /// </summary>
    /// <param name="minMinutes">Smallest choice, 0 or more.</param>
    /// <param name="maxMinutes">Largest choice, at most a day (1440).</param>
    /// <param name="stepMinutes">The slider snaps to multiples of this, counted from the minimum.</param>
    /// <param name="validate">Returns a reason (shown in red, confirm disabled) for a choice that isn't allowed, or null.</param>
    public static bool Open(ObenMod owner, string title, int minMinutes, int maxMinutes, int startMinutes,
        string confirmLabel, Action<int> onConfirm, Action onCancel = null, int stepMinutes = 1, Func<int, string> validate = null)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (onConfirm == null)
            throw new ArgumentNullException(nameof(onConfirm));
        if (!WaitMath.ValidRange(minMinutes, maxMinutes))
            throw new ArgumentOutOfRangeException(nameof(maxMinutes), $"Needs 0 <= minMinutes <= maxMinutes <= {WaitMath.MaxMinutes}.");
        if (stepMinutes < 1)
            throw new ArgumentOutOfRangeException(nameof(stepMinutes), "The step is at least one minute.");
        if (_open != null || !PlayerSleep.CanRest(out _))
            return false;

        var ui = WaitingUI.instance;
        if (ui == null || ui.IsActive())
            return false;

        var session = TimePickerSession.Find(ui);
        if (session == null)
        {
            GameEvents.Log.Warn("The sleep clock's controls were not found; the time picker can't open.");
            return false;
        }

        session.Owner = owner.Info.Id;
        session.Title = title ?? "";
        session.Min = minMinutes;
        session.Max = maxMinutes;
        session.Step = stepMinutes;
        session.Validate = validate;
        session.OnConfirm = onConfirm;
        session.OnCancel = onCancel;
        session.Show(ui, WaitMath.Snap(startMinutes, minMinutes, maxMinutes, stepMinutes), confirmLabel ?? "OK");
        _open = session;
        session.Refresh();

        owner.Track(Disposable.Create(() => CloseFor(session)));
        return true;
    }

    /// <summary>Closes the picker without an answer (calls the cancel callback). False when none is open.</summary>
    public static bool Close()
    {
        var ui = WaitingUI.instance;
        if (_open == null)
            return false;
        if (ui != null)
            ui.CloseTimeSetUI(); // Lands in Cancel through the patch.
        if (_open != null)
            Cancel();
        return true;
    }

    internal static void Init(ObenMod framework)
    {
        GameEvents.LevelChanging.Subscribe(framework, Drop);
        GameEvents.MainMenu.Subscribe(framework, Drop);
        framework.Track(Disposable.Create(() => CloseFor(_open)));
    }

    /// <summary>The game's sleep button was pressed while the picker is up.</summary>
    internal static void Confirm()
    {
        var session = _open;
        if (session == null)
            return;
        int minutes = session.Minutes();
        if (session.Refusal(minutes) != null)
        {
            session.Refresh();
            return;
        }

        _open = null;
        session.Restore();
        var ui = WaitingUI.instance;
        if (ui != null)
            ui.CloseTimeSetUI();
        Call(session, () => session.OnConfirm(minutes));
    }

    /// <summary>The game's cancel button (or anything else that closes the clock) was used.</summary>
    internal static void Cancel()
    {
        var session = _open;
        if (session == null)
            return;
        _open = null;
        session.Restore();
        if (session.OnCancel != null)
            Call(session, session.OnCancel);
    }

    internal static void Refresh() => _open?.Refresh();

    // The level is going away with its UI; there is nothing to restore.
    private static void Drop()
    {
        var session = _open;
        if (session == null)
            return;
        _open = null;
        if (session.OnCancel != null)
            Call(session, session.OnCancel);
    }

    // The owner or the framework unloads: put the clock back and close it, without calling anyone.
    private static void CloseFor(TimePickerSession session)
    {
        if (session == null || _open != session)
            return;
        _open = null;
        session.Restore();
        var ui = WaitingUI.instance;
        if (ui != null)
            ui.CloseTimeSetUI();
    }

    private static void Call(TimePickerSession session, Action action)
    {
        try { action(); }
        catch (Exception e) { GameEvents.Log.Error($"The time picker callback of {session.Owner} threw", e); }
    }
}
