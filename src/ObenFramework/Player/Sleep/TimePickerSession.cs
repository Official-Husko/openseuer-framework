// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenFramework.Player;

/// <summary>
/// One open <see cref="TimePicker"/>: the parts of the game's sleep clock it borrows, what they held before,
/// and the mod's settings. Everything changed is put back by <see cref="Restore"/>.
/// </summary>
internal sealed class TimePickerSession
{
    private static readonly AccessTools.FieldRef<WaitingUI, Slider> SliderField =
        AccessTools.FieldRefAccess<WaitingUI, Slider>("setAlarmSlider");
    private static readonly AccessTools.FieldRef<WaitingUI, GameObject> AlarmField =
        AccessTools.FieldRefAccess<WaitingUI, GameObject>("setAlarm");
    private static readonly AccessTools.FieldRef<WaitingUI, GameObject> ButtonsField =
        AccessTools.FieldRefAccess<WaitingUI, GameObject>("sleepButtons");
    private static readonly AccessTools.FieldRef<WaitingUI, TextMeshProUGUI> TextField =
        AccessTools.FieldRefAccess<WaitingUI, TextMeshProUGUI>("sleptTimeText");

    public string Owner;
    public string Title;
    public int Min;
    public int Max;
    public int Step;
    public Func<int, string> Validate;
    public Action<int> OnConfirm;
    public Action OnCancel;

    private Slider _slider;
    private GameObject _alarm;
    private TextMeshProUGUI _text;
    private TextMeshProUGUI _confirmText;
    private UnityEngine.UI.Button _confirm;

    private float _savedMin;
    private float _savedMax;
    private float _savedValue;
    private string _savedText;
    private string _savedConfirmText;
    private bool _savedInteractable;

    /// <summary>
    /// Collects the clock's slider, alarm row, text and sleep button, or null if a part is missing. The
    /// buttons are told apart by the method their click calls, which survives layout changes.
    /// </summary>
    public static TimePickerSession Find(WaitingUI ui)
    {
        var slider = SliderField(ui);
        var alarm = AlarmField(ui);
        var buttons = ButtonsField(ui);
        var text = TextField(ui);
        if (slider == null || alarm == null || buttons == null || text == null)
            return null;

        UnityEngine.UI.Button sleep = null;
        bool hasCancel = false;
        foreach (var button in buttons.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                string method = button.onClick.GetPersistentMethodName(i);
                if (method == nameof(WaitingUI.StartSleep))
                    sleep = button;
                else if (method == nameof(WaitingUI.CloseTimeSetUI))
                    hasCancel = true;
            }
        }
        var label = sleep != null ? sleep.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (label == null || !hasCancel)
            return null;

        return new TimePickerSession { _slider = slider, _alarm = alarm, _text = text, _confirm = sleep, _confirmText = label };
    }

    public void Show(WaitingUI ui, int startMinutes, string confirmLabel)
    {
        _savedMin = _slider.minValue;
        _savedMax = _slider.maxValue;
        _savedValue = _slider.value;
        _savedText = _text.text;
        _savedConfirmText = _confirmText.text;
        _savedInteractable = _confirm.interactable;
        try
        {
            _slider.minValue = Min * 60f;
            _slider.maxValue = Max * 60f;
            ui.ShowClock();
            _alarm.SetActive(true); // Hidden by the game when the player has no alarm clock.
            ui.ChangeSleepTimeAndSlider(startMinutes * 60f);
            _confirmText.text = confirmLabel;
        }
        catch
        {
            Restore();
            ui.CloseTimeSetUI();
            throw;
        }
    }

    /// <summary>The slider's value as whole minutes on the step grid.</summary>
    public int Minutes() => WaitMath.Snap(WaitMath.MinutesFromSlider(_slider.value), Min, Max, Step);

    /// <summary>The mod's reason to refuse <paramref name="minutes"/>, or null when it is fine.</summary>
    public string Refusal(int minutes)
    {
        if (Validate == null)
            return null;
        try
        {
            string reason = Validate(minutes);
            return string.IsNullOrEmpty(reason) ? null : reason;
        }
        catch (Exception e)
        {
            GameEvents.Log.Error($"The time picker check of {Owner} threw", e);
            return "Not available.";
        }
    }

    /// <summary>Writes the title and the choice on the clock and snaps the slider to the step.</summary>
    public void Refresh()
    {
        if (_text == null || _slider == null)
            return;
        int minutes = Minutes();
        string reason = Refusal(minutes);
        string text = Title + ": " + WaitMath.Clock(minutes);
        if (reason != null)
            text += "\n<color=#FF4444>" + reason + "</color>";
        _text.text = text;
        _confirm.interactable = reason == null;
        if (Step > 1 && _slider.value != minutes * 60f)
            _slider.value = minutes * 60f;
    }

    public void Restore()
    {
        if (_slider != null)
        {
            _slider.minValue = _savedMin;
            _slider.maxValue = _savedMax;
            _slider.value = _savedValue;
        }
        if (_confirmText != null)
            _confirmText.text = _savedConfirmText;
        if (_confirm != null)
            _confirm.interactable = _savedInteractable;
        if (_text != null)
            _text.text = _savedText;
    }
}
