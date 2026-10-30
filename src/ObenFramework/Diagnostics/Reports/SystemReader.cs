using System;
using System.Text.RegularExpressions;
using ObenLoader;
using ObenLoader.Mods;
using Oben.Reports;
using UnityEngine;

namespace ObenFramework.Diagnostics.Reports;

/// <summary>Fills a <see cref="SystemSnapshot"/> from Unity, the loader and the mod list. Main thread only.</summary>
internal static class SystemReader
{
    // Unity's system name has a double space in it ("Windows 10  (10.0.19045) 64bit").
    private static readonly Regex Spaces = new Regex(@"\s{2,}");

    public static SystemSnapshot Read()
    {
        var s = new SystemSnapshot
        {
            Written = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            Build = BuildStamp.Of(typeof(SystemReader).Assembly),
            Loader = Loader.Version,
            Game = Application.version,
            GameBuild = Loader.GameBuildId,
            GameCodeHash = Loader.GameCodeHash,
            GameExeHash = Loader.GameExeHash,
            Unity = Application.unityVersion,
            Os = Spaces.Replace(SystemInfo.operatingSystem, " ").Trim(),
            Language = Application.systemLanguage.ToString(),
            Cpu = SystemInfo.processorType,
            CpuCores = SystemInfo.processorCount,
            CpuMhz = SystemInfo.processorFrequency,
            Gpu = SystemInfo.graphicsDeviceName,
            GpuVendor = SystemInfo.graphicsDeviceVendor,
            GpuApi = SystemInfo.graphicsDeviceVersion,
            GpuMemoryMb = SystemInfo.graphicsMemorySize,
            RamMb = SystemInfo.systemMemorySize,
            Display = $"{Screen.width}x{Screen.height}, {Mode(Screen.fullScreenMode)}, {Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value)} Hz",
        };
        WineInfo.Fill(s);
        foreach (var mod in ModRegistry.Mods)
            s.Mods.Add(new ModEntry { Id = mod.Id, Name = mod.Manifest.Name, Version = mod.Manifest.Version, State = mod.State.ToString() });
        try
        {
            s.BepInEx = BepInExScan.Read(LoaderPaths.GameDir);
        }
        catch (Exception e)
        {
            BugReports.Log?.Warn("Couldn't look at BepInEx: " + e.Message);
        }
        return s;
    }

    private static string Mode(FullScreenMode mode) => mode switch
    {
        FullScreenMode.ExclusiveFullScreen => "fullscreen",
        FullScreenMode.FullScreenWindow => "borderless fullscreen",
        FullScreenMode.MaximizedWindow => "maximized window",
        _ => "windowed",
    };
}
