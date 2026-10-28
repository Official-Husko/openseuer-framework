// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Events;
using ObenUIKit;
using UnityEngine;

namespace GameTweaks.Camera;

/// <summary>
/// A key (F6 by default, rebindable on the Hotkeys page) switches between first and third person, and
/// optionally the mouse wheel zooms in and out like the game's old third-person camera. Third person is a
/// mode the game no longer uses, so the body, its animations and clipping through walls may look rough.
/// While off there are no patches and no per-frame work.
/// </summary>
internal sealed class ThirdPersonTweak : TweakFeature
{
    public const string Key = "ThirdPerson";

    public override string Id => Key;
    public override string Name => "Third-person camera";
    public override string Description =>
        "F6 switches between first and third person (rebind it on the Hotkeys page). The game no longer uses third person, so the body can look rough.";

    public override void Apply(TweaksConfig config)
    {
        CameraView.Distance = TweakMath.Factor(config.CameraDistance, 0.5f, 10f, 2.5f);
        CameraView.SetScrollZoom(config.ScrollZoom);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Hotkeys.Register(this, "camera", "Switch between first and third person", KeyCode.F6, Toggle);
        GameEvents.SceneLoaded.Subscribe(this, _ => CameraView.SceneChanged());
    }

    public override void OnUpdate() => CameraView.Enforce();

    protected override void OnDisable() => CameraView.Reset();

    private static void Toggle()
    {
        if (!CameraView.Toggle())
            UiKit.Toast("The camera has to stay in first person here.", "Camera");
    }
}
