using System;
using System.Collections;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DevBridge.Commands;

internal static class ScreenshotCommand
{
    [BridgeCommand("screenshot", "Capture the game window as base64 image data.",
        Params = new[] { "scale?: 0.1-1, default 0.5", "format?: jpg (default) or png", "quality?: jpg quality, default 80" })]
    private static object Capture(JObject p)
    {
        var result = new Deferred();
        DevBridgeMod.Instance.StartCoroutine(CaptureAtEndOfFrame(p, result));
        return result;
    }

    private static IEnumerator CaptureAtEndOfFrame(JObject p, Deferred result)
    {
        yield return new WaitForEndOfFrame();

        Texture2D shot = null;
        try
        {
            shot = Downscale(ScreenCapture.CaptureScreenshotAsTexture(), Mathf.Clamp(p.Float("scale", 0.5f), 0.1f, 1f));
            bool png = string.Equals(p.Str("format", "jpg"), "png", StringComparison.OrdinalIgnoreCase);
            byte[] bytes = png ? shot.EncodeToPNG() : shot.EncodeToJPG(Mathf.Clamp(p.Int("quality", 80), 10, 100));

            result.Complete(new JObject
            {
                ["mimeType"] = png ? "image/png" : "image/jpeg",
                ["width"] = shot.width,
                ["height"] = shot.height,
                ["scene"] = SceneManager.GetActiveScene().name,
                ["data"] = Convert.ToBase64String(bytes),
            });
        }
        catch (Exception e)
        {
            result.Fail(e);
        }
        finally
        {
            if (shot != null)
                Object.Destroy(shot);
        }
    }

    private static Texture2D Downscale(Texture2D source, float scale)
    {
        if (scale >= 0.999f)
            return source;

        int w = Mathf.Max(1, (int)(source.width * scale));
        int h = Mathf.Max(1, (int)(source.height * scale));
        var rt = RenderTexture.GetTemporary(w, h, 0);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var small = new Texture2D(w, h, TextureFormat.RGB24, false);
            small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            small.Apply();
            return small;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(source);
        }
    }
}
