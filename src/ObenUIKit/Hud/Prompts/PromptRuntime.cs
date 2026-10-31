using System;
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Ui;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Runtime;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// Runs prompts and guides: decides which prompt shows (the newest whose conditions hold), draws it in the
/// game's tutorial prompt or the kit's own panel, reads its key, and steps guides along. Its per-frame
/// component is on only while a prompt or guide exists; conditions are asked five times a second.
/// </summary>
internal sealed partial class PromptRuntime : IDisposable
{
    private const float CheckInterval = 0.2f;

    /// <summary>Prompts one owner can have at once; past that its oldest goes.</summary>
    public const int MaxPerOwner = 16;

    private readonly KitContext _kit;
    private readonly RectTransform _root;
    private readonly List<HudPrompt> _prompts = new List<HudPrompt>();
    private readonly List<HudGuide> _guides = new List<HudGuide>();
    private readonly HashSet<ITracker> _owners = new HashSet<ITracker>();
    private readonly TutorialPrompt _native;
    private readonly PromptPanel _panel;
    private readonly PromptTick _tick;
    private HudPrompt _shown;
    private HudPrompt _last;
    private int _shownVersion;
    private KeyCode _shownKey;
    private bool _shownNative;
    private IDisposable _capture;
    private float _nextCheck;
    private int _serial;

    public static PromptRuntime Current { get; private set; }

    public PromptRuntime(KitContext kit)
    {
        _kit = kit;
        _root = UiParts.Stretch(UiParts.Child(kit.Root.Rect, "Prompts"));
        _root.SetAsFirstSibling();
        _native = new TutorialPrompt(kit.Log);
        _panel = new PromptPanel(_root);
        _tick = kit.Root.Root.AddComponent<PromptTick>();
        _tick.Init(this, kit.Log);
        Current = this;
    }

    public IReadOnlyList<HudPrompt> Prompts => _prompts;

    public IReadOnlyList<HudGuide> Guides => _guides;

    public HudPrompt Shown => _shown;

    public bool ShownNatively => _shown != null && _shownNative;

    /// <param name="reuse">
    /// Asked again (a mod calling every frame): the same owner, text, key and place gives the same prompt.
    /// Guides keep their own prompts apart.
    /// </param>
    public HudPrompt Add(ITracker owner, string text, PromptKey key, PromptOptions options, bool reuse = false)
    {
        HudPrompt oldest = null;
        int count = 0;
        foreach (var existing in _prompts)
        {
            if (existing.Owner != owner)
                continue;
            if (reuse && existing.Reusable && existing.Text == text && existing.Key.Same(key) && existing.Options.Near == options.Near)
                return existing;
            oldest ??= existing;
            count++;
        }
        if (count >= MaxPerOwner)
            Remove(oldest);
        WarnUnbound(key, text);

        Own(owner);
        var prompt = new HudPrompt(owner, text, key, options, ++_serial) { Reusable = reuse };
        prompt.Wanted = Wanted(prompt);
        _prompts.Add(prompt);
        _tick.enabled = true;
        return prompt;
    }

    public void RemoveOwner(ITracker owner)
    {
        for (int i = _prompts.Count - 1; i >= 0; i--)
        {
            if (i < _prompts.Count && _prompts[i].Owner == owner)
                Remove(_prompts[i]);
        }
    }

    // A misspelt action has no key on the keyboard or the pad, and the prompt would show a blank key.
    private void WarnUnbound(PromptKey key, string text)
    {
        var input = InputManager.instance;
        if (key.Action == null || input == null || input.keyBindings == null)
            return;
        if (input.GetKeyCode(key.Action, ignoreController: true) == KeyCode.None && input.keyBindings.CheckKeyController(key.Action) == KeyCode.None)
            _kit.Log.Warn($"The prompt '{text}' asks for the action '{key.Action}', which has no key; check the name (\"Interact\", \"Jump\", \"Inventory\"...)");
    }

    public void Remove(HudPrompt prompt)
    {
        prompt.IsActive = false;
        if (!_prompts.Remove(prompt))
            return;
        if (prompt == _shown)
            Present(null);
    }

    public bool IsShowing(HudPrompt prompt) => prompt == _shown;

    public void Add(HudGuide guide)
    {
        Own(guide.Owner);
        for (int i = _guides.Count - 1; i >= 0; i--)
        {
            if (_guides[i].Id == guide.Id)
                _guides[i].Stop();
        }
        _guides.Add(guide);
        _tick.enabled = true;
    }

    public void Remove(HudGuide guide) => _guides.Remove(guide);

    /// <summary>Called every frame while a prompt or guide exists.</summary>
    public void Tick(float delta)
    {
        bool blocked = Blocked();
        // Keys first, for the prompt that was on screen until now: a key that opens a menu (Map, Inventory)
        // hides the prompt in the same frame, and the press must still count.
        if (_shown != null)
            Keys(_shown);

        for (int i = _guides.Count - 1; i >= 0; i--)
        {
            if (i < _guides.Count)
                _guides[i].Tick(delta, blocked);
        }

        float now = Time.unscaledTime;
        if (now >= _nextCheck)
        {
            _nextCheck = now + CheckInterval;
            for (int i = 0; i < _prompts.Count; i++)
            {
                var prompt = _prompts[i];
                // The object it was tied to is gone (a level change): the prompt has nothing to point at any more.
                if (prompt.Options.Near == null && !ReferenceEquals(prompt.Options.Near, null))
                {
                    Remove(prompt);
                    i--;
                    continue;
                }
                prompt.Wanted = Wanted(prompt);
            }
        }

        Present(blocked || !Hud.ModPromptsShown ? null : Pick());
        if (_shown != null)
        {
            _shown.Shown += delta;
            if (_shown.Options.Seconds > 0f && _shown.Shown >= _shown.Options.Seconds)
                Remove(_shown);
        }
        _tick.enabled = _prompts.Count > 0 || _guides.Count > 0;
    }

    private void Keys(HudPrompt prompt)
    {
        var options = prompt.Options;
        if (prompt.Pressed == null && options.OnPress == null)
            return;
        if (!prompt.IsActive || UiState.IsTyping || !prompt.Key.Pressed())
            return;

        prompt.Pressed?.Invoke();
        if (options.OnPress != null)
        {
            try { options.OnPress(); }
            catch (Exception e) { _kit.Log.Error($"A prompt's key action failed ('{prompt.Text}')", e); }
        }
        if (options.Once)
            Remove(prompt);
    }

    // Nothing of ours shows over windows, menus, the pause menu, dialogue or loading, while the game is paused,
    // or with the HUD hidden.
    private static bool Blocked() =>
        HudGate.Blocked || HudGate.GameUiHidden || !WorldState.IsReady || UiState.IsDialogueActive || UiState.IsConsoleOpen || UiState.IsPaused;

    private HudPrompt Pick()
    {
        for (int i = _prompts.Count - 1; i >= 0; i--)
        {
            if (_prompts[i].Wanted)
                return _prompts[i];
        }
        return null;
    }

    private bool Wanted(HudPrompt prompt)
    {
        var options = prompt.Options;
        if (options.Near != null)
        {
            var body = PlayerLocator.instance;
            if (body == null)
                return false;
            if ((body.transform.position - options.Near.position).sqrMagnitude > options.Distance * options.Distance)
                return false;
        }
        else if (!ReferenceEquals(options.Near, null))
        {
            return false;
        }
        if (options.When == null)
            return true;
        try { return options.When(); }
        catch (Exception e)
        {
            _kit.Log.Error($"A prompt's condition failed; the prompt stays hidden ('{prompt.Text}')", e);
            options.When = () => false;
            return false;
        }
    }
}
