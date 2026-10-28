// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>
/// The game-side half of a <see cref="CustomInteraction"/>: an <c>Interactable</c> like the game's own, so
/// the crosshair, the prompt and the interact key treat it exactly the same. It saves nothing.
/// </summary>
internal sealed class ModInteractable : Interactable
{
    internal CustomInteraction Handle;
    private bool _reasonFailed;

    public override void UpdateUseMessage()
    {
        if (UseText.instance == null)
            return;
        string reason = Reason();
        if (reason == null)
            base.UpdateUseMessage();
        else
            UseText.instance.ChangeUseText(title, reason, showUseText: true, redColor: true);
    }

    public override void Interact()
    {
        if (Handle == null)
            return;
        if (Reason() != null)
        {
            if (UseText.instance != null)
                UseText.instance.WarningUseEffect();
            return;
        }
        try
        {
            Handle.OnUse();
        }
        catch (Exception e)
        {
            GameEvents.Log.Error($"The interaction '{title}' of {Handle.OwnerId} threw", e);
        }
    }

    // The reason is asked for every frame the crosshair rests on the object; a throwing one is logged once.
    private string Reason()
    {
        var reason = Handle != null ? Handle.Reason : null;
        if (reason == null)
            return null;
        try
        {
            string text = reason();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch (Exception e)
        {
            if (!_reasonFailed)
            {
                _reasonFailed = true;
                GameEvents.Log.Error($"The reason check of interaction '{title}' threw", e);
            }
            return null;
        }
    }
}
