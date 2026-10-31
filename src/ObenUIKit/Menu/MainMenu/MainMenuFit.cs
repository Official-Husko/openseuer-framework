using System;
using UnityEngine;

namespace ObenUIKit.Menu;

/// <summary>
/// How many rows of mod buttons fit under the main menu's grid. The menu is a sign in the scene seen through
/// the menu camera, so this is measured on screen: a row fits while its buttons end above the bottom edge,
/// less a strip for the status line. It is measured while the sign is in view and remembered for when the
/// camera is over another page (Load Game, Options). The camera keeps its height of view at any aspect, so
/// the answer hardly changes with the resolution.
/// </summary>
internal static class MainMenuFit
{
    private const int MaxRows = 6;
    private const float BottomStrip = 0.03f;

    private static int _rows = 2;

    /// <summary>
    /// Rows that fit under <paramref name="bottomRow"/> (the grid's bottom row, in <paramref name="parent"/>'s
    /// space) at <paramref name="rowStep"/> apart, for buttons the size of <paramref name="sample"/>. At least one.
    /// </summary>
    public static int Rows(RectTransform sample, Transform parent, Vector3 bottomRow, float rowStep)
    {
        var camera = Camera.main;
        if (camera == null || !OnScreen(camera, sample))
            return _rows;

        var corners = new Vector3[4];
        sample.GetWorldCorners(corners);
        float half = Mathf.Abs(camera.WorldToScreenPoint(corners[1]).y - camera.WorldToScreenPoint(corners[0]).y) * 0.5f;
        float floor = camera.pixelHeight * BottomStrip;
        int rows = 0;
        while (rows < MaxRows)
        {
            var centre = parent.TransformPoint(new Vector3(bottomRow.x, bottomRow.y - (rows + 1) * rowStep, bottomRow.z));
            var point = camera.WorldToScreenPoint(centre);
            if (point.z <= 0f || point.y - half < floor)
                break;
            rows++;
        }
        _rows = Math.Max(1, rows);
        return _rows;
    }

    /// <summary>The whole rectangle is inside the camera's view.</summary>
    public static bool OnScreen(Camera camera, RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            var point = camera.WorldToScreenPoint(corner);
            if (point.z <= 0f || point.x < 0f || point.y < 0f || point.x > camera.pixelWidth || point.y > camera.pixelHeight)
                return false;
        }
        return true;
    }
}
