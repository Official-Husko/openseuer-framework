using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;

namespace DevBridge.Commands;

/// <summary>The player camera's first and third person through the framework's <see cref="CameraView"/>.</summary>
internal static class CameraCommands
{
    [BridgeCommand("player.camera", "The camera view and what mods ask of it. mode= switches first or third person for the bridge; body= keeps the body drawn in first person.",
        Params = new[]
        {
            "mode?: first or third",
            "distance?: metres behind the head for third person (default 2.5)",
            "body?: true keeps the body drawn in first person, false lets go",
        })]
    private static object Camera(JObject p)
    {
        var owner = DevBridgeMod.Instance ?? throw new InvalidOperationException("The bridge isn't loaded.");
        string mode = p.Str("mode", null);
        if (mode != null || p["body"] != null)
        {
            if (!CameraView.IsAvailable)
                throw new InvalidOperationException("There is no player camera right now (main menu or loading).");
            if (mode == "third")
            {
                if (!CameraView.SetThirdPerson(owner, p.Float("distance", CameraView.DefaultDistance)))
                    throw new InvalidOperationException("Third person isn't allowed here (a camera trigger, building mode, or the camera hooks are off).");
            }
            else if (mode == "first")
                CameraView.SetFirstPerson(owner);
            else if (mode != null)
                throw new ArgumentException("mode is first or third");
            if (p["body"] == null)
                return State();
            if (!p.Bool("body"))
                CameraView.ClearFirstPersonBody(owner);
            else if (!CameraView.SetFirstPersonBody(owner))
                throw new InvalidOperationException("The body hook isn't in place.");
        }
        return State();
    }

    private static JObject State()
    {
        var result = new JObject
        {
            ["available"] = CameraView.IsAvailable,
            ["firstPerson"] = CameraView.IsFirstPerson,
            ["thirdPersonAllowed"] = CameraView.ThirdPersonAllowed,
            ["thirdPersonWanted"] = CameraView.ThirdPersonWanted,
            ["heldBack"] = CameraView.ThirdPersonHeldBack,
            ["scrollZoom"] = CameraView.ScrollZoomAllowed,
            ["bodyShown"] = CameraView.FirstPersonBodyShown,
        };
        var view = ThirdPersonCameraCollision.instance;
        if (view != null)
        {
            result["game"] = new JObject
            {
                ["disableThirdPerson"] = view.disableThirdPerson,
                ["firstpersonOnly"] = view.firstpersonOnly,
                ["currentDistance"] = view.currentDistance,
                ["distance"] = view.distance,
            };
        }
        return result;
    }
}
