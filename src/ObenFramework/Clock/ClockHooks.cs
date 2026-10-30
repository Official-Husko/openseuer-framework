using System;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Clock;

/// <summary>
/// Connects <see cref="GameClock"/>'s events to TimeOfDayAzure's static events. A game event is only
/// subscribed to while a mod listens to the matching clock event, and all of them are removed when the
/// framework unloads. The game invokes its handlers inside one try/catch, so nothing here may throw.
/// Ticks while a save is written or loaded (<see cref="SaveGate.IsBusy"/>) are not passed on.
/// </summary>
internal static class ClockHooks
{
    private static bool _secondHooked, _minuteHooked, _dayHooked;
    private static int _lastHour = -1;
    private static int _lastMinuteOfDay = -1;

    public static void Init(ObenMod framework)
    {
        // A new scene starts on its own time and a load sets the save's: neither is time passing.
        GameEvents.SceneLoaded.Subscribe(framework, ForgetLastTick);
        GameEvents.Loaded.Subscribe(framework, ForgetLastTick);
        framework.Track(Disposable.Create(() =>
        {
            TimeOfDayAzure.SecondsPassed -= OnSeconds;
            TimeOfDayAzure.MinutePassed -= OnMinute;
            TimeOfDayAzure.DayChanged -= OnDay;
            _secondHooked = _minuteHooked = _dayHooked = false;
        }));
        Refresh();
    }

    /// <summary>Hooks or unhooks the game events to match who is subscribed.</summary>
    public static void Refresh()
    {
        Sync(ref _secondHooked, GameClock.SecondPassed.HasSubscribers, () => TimeOfDayAzure.SecondsPassed += OnSeconds, () => TimeOfDayAzure.SecondsPassed -= OnSeconds);
        Sync(ref _dayHooked, GameClock.DayChanged.HasSubscribers, () => TimeOfDayAzure.DayChanged += OnDay, () => TimeOfDayAzure.DayChanged -= OnDay);

        bool wantMinute = GameClock.MinutePassed.HasSubscribers || GameClock.HourChanged.HasSubscribers || GameClock.Countdowns.Count > 0;
        if (wantMinute && !_minuteHooked)
        {
            _lastHour = GameClock.IsAvailable ? GameClock.Hour : -1;
            _lastMinuteOfDay = GameClock.IsAvailable ? GameClock.Hour * 60 + GameClock.Minute : -1;
        }
        Sync(ref _minuteHooked, wantMinute, () => TimeOfDayAzure.MinutePassed += OnMinute, () => TimeOfDayAzure.MinutePassed -= OnMinute);
    }

    // The next minute tick only records the time (no hour change, one minute for countdowns).
    private static void ForgetLastTick()
    {
        _lastHour = -1;
        _lastMinuteOfDay = -1;
    }

    private static void Sync(ref bool hooked, bool wanted, Action hook, Action unhook)
    {
        if (wanted == hooked)
            return;
        if (wanted)
            hook();
        else
            unhook();
        hooked = wanted;
    }

    private static void OnSeconds(int seconds)
    {
        try
        {
            if (!SaveGate.IsBusy)
                GameClock.SecondPassed.Raise(seconds, GameEvents.Log);
        }
        catch (Exception e) { GameEvents.Log.Error("SecondPassed failed", e); }
    }

    private static void OnMinute(int _)
    {
        try
        {
            if (!GameClock.IsAvailable)
                return;
            // A level change or load moves the clock: the new scene runs on its own time for a few frames
            // before the save sets it. Those ticks aren't time passing, so skip them and count again from
            // the restored time without an hour change.
            if (SaveGate.IsBusy)
            {
                ForgetLastTick();
                return;
            }

            // The game's own currentTimeAndDay is a minute behind in here; the controller is current.
            int hour = GameClock.Hour;
            int minuteOfDay = hour * 60 + GameClock.Minute;
            int passed = MinuteCountdowns.MinutesBetween(_lastMinuteOfDay, minuteOfDay);
            _lastMinuteOfDay = minuteOfDay;
            GameClock.MinutePassed.Raise(GameClock.Minute, GameEvents.Log);
            if (hour != _lastHour)
            {
                bool known = _lastHour >= 0;
                _lastHour = hour;
                if (known)
                    GameClock.HourChanged.Raise(hour, GameEvents.Log);
            }
            if (GameClock.Countdowns.Count > 0)
            {
                GameClock.TickCountdowns(passed);
                if (GameClock.Countdowns.Count == 0)
                    Refresh();
            }
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("MinutePassed failed", e);
        }
    }

    private static void OnDay(int day)
    {
        try { GameClock.DayChanged.Raise(day, GameEvents.Log); }
        catch (Exception e) { GameEvents.Log.Error("DayChanged failed", e); }
    }
}
