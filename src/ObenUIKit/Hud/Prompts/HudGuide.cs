using System;
using System.Collections.Generic;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// A short walk-through in the game's own tutorial prompt, one step at a time, the way the game teaches
/// moving and interacting: each step names a key and waits for it (or for a condition, or a few seconds),
/// then the next one follows after a short pause. Make one with <see cref="Hud.Guide"/>, add steps, and
/// <see cref="Start"/> it. It stops when its owner unloads.
/// </summary>
public sealed class HudGuide
{
    private sealed class GuideStep
    {
        public string Text;
        public PromptKey Key;
        public Func<bool> Done;
        public float Seconds;
    }

    private readonly List<GuideStep> _steps = new List<GuideStep>();
    private HudPrompt _prompt;
    private float _wait;
    private bool _pressed;
    private float _onScreen;

    internal HudGuide(ITracker owner, string id)
    {
        Owner = owner;
        Id = id;
    }

    internal ITracker Owner { get; }

    public string Id { get; }

    /// <summary>Seconds between one step and the next (the game uses about two).</summary>
    public float Pause { get; set; } = 1.5f;

    /// <summary>How many steps it has.</summary>
    public int Count => _steps.Count;

    /// <summary>The step showing or about to show; -1 before the start, <see cref="Count"/> once finished.</summary>
    public int Current { get; private set; } = -1;

    public bool IsRunning { get; private set; }

    public bool IsFinished => Current >= _steps.Count && _steps.Count > 0;

    /// <summary>A step came up; the payload is its index.</summary>
    public event Action<int> StepStarted;

    /// <summary>The player went through every step.</summary>
    public event Action Finished;

    /// <summary>
    /// A step that shows one of the game's actions and is done when the player presses it, or, with
    /// <paramref name="done"/>, when that turns true (the key is then only shown).
    /// </summary>
    public HudGuide Step(string text, string action, Func<bool> done = null) =>
        Add(text, PromptKey.ForAction(action ?? throw new ArgumentNullException(nameof(action))), done, 0f);

    /// <summary>A step that shows a key and is done when the player presses it, or, with <paramref name="done"/>, when that turns true.</summary>
    public HudGuide Step(string text, KeyCode key, Func<bool> done = null) => Add(text, PromptKey.ForKey(key), done, 0f);

    /// <summary>A step with text only, done when <paramref name="done"/> turns true or after <paramref name="seconds"/> on screen.</summary>
    public HudGuide Step(string text, Func<bool> done, float seconds = 0f)
    {
        if (done == null && seconds <= 0f)
            throw new ArgumentException("A step without a key needs a condition or a time.", nameof(done));
        return Add(text, PromptKey.None(), done, seconds);
    }

    /// <summary>Starts from the first step (again, if it ran before).</summary>
    public HudGuide Start()
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("The guide has no steps.");
        var runtime = PromptRuntime.Current ?? throw new InvalidOperationException("ObenUIKit isn't loaded.");
        Stop();
        Current = 0;
        _wait = 0.3f;
        IsRunning = true;
        runtime.Add(this);
        return this;
    }

    /// <summary>Takes the guide off the screen where it is.</summary>
    public void Stop()
    {
        HidePrompt();
        if (IsRunning)
            PromptRuntime.Current?.Remove(this);
        IsRunning = false;
    }

    /// <summary>Counts the step showing as done and moves on.</summary>
    public void Skip()
    {
        if (IsRunning && _prompt != null)
            _pressed = true;
    }

    // Called every frame by the prompt runtime while the guide runs. Timers only run while the HUD shows.
    internal void Tick(float delta, bool blocked)
    {
        if (!IsRunning || blocked)
            return;
        if (_prompt == null)
        {
            _wait -= delta;
            if (_wait <= 0f)
                ShowStep();
            return;
        }

        var step = _steps[Current];
        if (_prompt.IsShowing)
            _onScreen += delta;
        if (_pressed || (step.Seconds > 0f && _onScreen >= step.Seconds) || Check(step))
            NextStep();
    }

    private HudGuide Add(string text, PromptKey key, Func<bool> done, float seconds)
    {
        if (IsRunning)
            throw new InvalidOperationException("Add steps before the guide starts.");
        _steps.Add(new GuideStep { Text = text ?? "", Key = key, Done = done, Seconds = seconds });
        return this;
    }

    private void ShowStep()
    {
        var step = _steps[Current];
        _pressed = false;
        _onScreen = 0f;
        // Guides teach the game's own keys, so the key isn't kept from the game.
        _prompt = PromptRuntime.Current.Add(Owner, step.Text, step.Key, new PromptOptions { Capture = false });
        if (step.Done == null && !step.Key.IsNone)
            _prompt.Pressed = () => _pressed = true;
        Raise(StepStarted, Current);
    }

    private void NextStep()
    {
        HidePrompt();
        Current++;
        _wait = Pause;
        if (Current < _steps.Count)
            return;
        IsRunning = false;
        PromptRuntime.Current?.Remove(this);
        try { Finished?.Invoke(); }
        catch (Exception e) { Runtime.KitRuntime.Current?.Kit.Log.Error($"Guide '{Id}': a Finished handler threw", e); }
    }

    private bool Check(GuideStep step)
    {
        if (step.Done == null)
            return false;
        try { return step.Done(); }
        catch (Exception e)
        {
            Runtime.KitRuntime.Current?.Kit.Log.Error($"Guide '{Id}': the condition of step {Current + 1} failed; the step is skipped", e);
            return true;
        }
    }

    private void HidePrompt()
    {
        if (_prompt == null)
            return;
        _prompt.Hide();
        _prompt = null;
    }

    private void Raise(Action<int> handler, int value)
    {
        try { handler?.Invoke(value); }
        catch (Exception e) { Runtime.KitRuntime.Current?.Kit.Log.Error($"Guide '{Id}': a StepStarted handler threw", e); }
    }
}
