// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.NewGames;
using ObenLoader.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit;

/// <summary>
/// Draws a prompt in the game's own tutorial prompt under the crosshair (Game UI/Tutorial/Other): its text,
/// and the key in the game's key box, mouse picture or pad button. The game only uses it during its own
/// tutorial steps; while one of those is up (<see cref="TutorialGuide.Active"/>) it is the game's, and
/// prompts go to the kit's own panel instead.
/// </summary>
internal sealed class TutorialPrompt
{
    private static readonly AccessTools.FieldRef<Tutorial, GameObject> OtherRef = AccessTools.FieldRefAccess<Tutorial, GameObject>("Other");
    private static readonly AccessTools.FieldRef<Tutorial, TextMeshProUGUI> TextRef = AccessTools.FieldRefAccess<Tutorial, TextMeshProUGUI>("TutorialText");
    private static readonly AccessTools.FieldRef<Tutorial, InputUI> KeyRef = AccessTools.FieldRefAccess<Tutorial, InputUI>("inputUI");
    private static readonly AccessTools.FieldRef<Tutorial, Image> MouseImageRef = AccessTools.FieldRefAccess<Tutorial, Image>("MouseImage");
    private static readonly AccessTools.FieldRef<Tutorial, Sprite> Mouse0Ref = AccessTools.FieldRefAccess<Tutorial, Sprite>("Mouse0");
    private static readonly AccessTools.FieldRef<Tutorial, Sprite> Mouse1Ref = AccessTools.FieldRefAccess<Tutorial, Sprite>("Mouse1");
    private static readonly AccessTools.FieldRef<Tutorial, Sprite> Mouse2Ref = AccessTools.FieldRefAccess<Tutorial, Sprite>("Mouse2");
    private static readonly AccessTools.FieldRef<Tutorial, AudioSource> SoundRef = AccessTools.FieldRefAccess<Tutorial, AudioSource>("Sound");
    private static readonly AccessTools.FieldRef<InputUI, GameObject> SmallKeyRef = AccessTools.FieldRefAccess<InputUI, GameObject>("button");
    private static readonly AccessTools.FieldRef<InputUI, GameObject> LargeKeyRef = AccessTools.FieldRefAccess<InputUI, GameObject>("largeButton");
    private static readonly AccessTools.FieldRef<InputUI, TextMeshProUGUI> SmallTextRef = AccessTools.FieldRefAccess<InputUI, TextMeshProUGUI>("buttonText");
    private static readonly AccessTools.FieldRef<InputUI, TextMeshProUGUI> LargeTextRef = AccessTools.FieldRefAccess<InputUI, TextMeshProUGUI>("buttonTextLarge");

    private readonly ModLogger _log;
    private Tutorial _drawnOn;
    private bool _broken;

    public TutorialPrompt(ModLogger log)
    {
        _log = log;
    }

    /// <summary>The scene has the tutorial prompt and the game isn't using it.</summary>
    public bool Free => !_broken && global::Tutorial.instance != null && !TutorialGuide.Active;

    /// <summary>Our prompt is on the game's panel right now.</summary>
    public bool Showing => _drawnOn != null;

    /// <summary>The panel we drew on is still up (the game's tutorial code can switch it off on its own).</summary>
    public bool Visible => _drawnOn != null && OtherRef(_drawnOn) != null && OtherRef(_drawnOn).activeSelf;

    public bool Show(string text, PromptKey key, bool sound)
    {
        var tutorial = global::Tutorial.instance;
        if (tutorial == null || _broken)
            return false;
        try
        {
            var other = OtherRef(tutorial);
            TextRef(tutorial).text = text;
            DrawKey(tutorial, key);
            other.SetActive(true);
            _drawnOn = tutorial;
            if (sound)
            {
                var chime = SoundRef(tutorial);
                if (chime != null)
                    chime.Play();
            }
            return true;
        }
        catch (Exception e)
        {
            _broken = true;
            _log.Error("The game's tutorial prompt couldn't be used; prompts use the kit's own panel from now on", e);
            return false;
        }
    }

    /// <summary>Takes our prompt off the game's panel, unless the game has taken the panel over meanwhile.</summary>
    public void Hide()
    {
        var tutorial = _drawnOn;
        _drawnOn = null;
        if (tutorial == null || TutorialGuide.Active)
            return;
        try { OtherRef(tutorial).SetActive(false); }
        catch (Exception e) { _log.Error("Hiding the tutorial prompt failed", e); }
    }

    /// <summary>The game started one of its own tutorial steps on the panel; it's no longer ours.</summary>
    public void Lose() => _drawnOn = null;

    private static void DrawKey(Tutorial tutorial, PromptKey key)
    {
        var keyUi = KeyRef(tutorial);
        var mouse = MouseImageRef(tutorial);
        mouse.gameObject.SetActive(false);
        keyUi.Hide();
        if (key.IsNone)
            return;

        var code = key.Current;
        var sprite = code == KeyCode.Mouse0 ? Mouse0Ref(tutorial) : code == KeyCode.Mouse1 ? Mouse1Ref(tutorial)
            : code == KeyCode.Mouse2 ? Mouse2Ref(tutorial) : null;
        if (sprite != null)
        {
            mouse.sprite = sprite;
            mouse.gameObject.SetActive(true);
            return;
        }
        if (key.Action != null)
        {
            // The game's own drawing: keyboard key boxes or pad button pictures, whichever the player uses.
            keyUi.UpdateUI(key.Action);
            return;
        }

        string shown = key.Display();
        bool small = shown.Length == 1;
        (small ? SmallTextRef(keyUi) : LargeTextRef(keyUi)).text = shown;
        (small ? SmallKeyRef(keyUi) : LargeKeyRef(keyUi)).SetActive(true);
    }
}
