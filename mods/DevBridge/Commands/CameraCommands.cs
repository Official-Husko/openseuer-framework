using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Npcs;
using ObenFramework.Player;
using UnityEngine;

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

    private static IDisposable _lock;
    private static GameObject _marker;

    [BridgeCommand("player.lookat", "Lock the view onto an NPC or a point with CameraLook, or let go (release=true).",
        Params = new[] { "npc?: NPC name", "x?,y?,z? or pos?: a point instead", "instantly?: turn at once, default false",
            "speed?: degrees per second, default 100", "release?: true lets go of the bridge's lock" })]
    private static object LookAt(JObject p)
    {
        var owner = DevBridgeMod.Instance ?? throw new InvalidOperationException("The bridge isn't loaded.");
        ReleaseLock();
        if (!p.Bool("release", false))
        {
            Transform target;
            if (p["npc"] != null)
            {
                var npc = Npcs.Find(p.Str("npc")) ?? throw new ArgumentException("No such NPC.");
                var live = Npcs.LiveObject(npc.Id);
                target = live != null ? live.transform : throw new InvalidOperationException("That NPC isn't in this scene.");
            }
            else
            {
                var point = p.Position() ?? throw new ArgumentException("Give npc, or x/y/z or pos.");
                _marker = new GameObject("DevBridge look-at marker");
                _marker.transform.position = point;
                target = _marker.transform;
            }
            _lock = CameraLook.LockOn(owner, target, p.Bool("instantly", false), p.Float("speed", CameraLook.GameTurnSpeed));
        }
        return new JObject { ["locked"] = CameraLook.IsLocked, ["target"] = CameraLook.Target != null ? CameraLook.Target.name : null };
    }

    private static void ReleaseLock()
    {
        _lock?.Dispose();
        _lock = null;
        if (_marker != null)
            UnityEngine.Object.Destroy(_marker);
        _marker = null;
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
