using System;
using System.Runtime.InteropServices;
using Oben.Reports;
using UnityEngine;

namespace ObenFramework.Diagnostics.Reports;

/// <summary>
/// Under Wine and Proton the game believes it runs on Windows 10. Wine's ntdll has two functions real Windows
/// doesn't: wine_get_version and wine_get_host_version, which name the Wine build and the real system below it.
/// </summary>
internal static class WineInfo
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetVersion();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GetHostVersion(out IntPtr system, out IntPtr release);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string name);

    [DllImport("kernel32", CharSet = CharSet.Ansi, BestFitMapping = false)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);

    /// <summary>Sets <see cref="SystemSnapshot.Wine"/> and <see cref="SystemSnapshot.Host"/>; leaves them null on Windows.</summary>
    public static void Fill(SystemSnapshot s)
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer)
            return;
        try
        {
            IntPtr ntdll = GetModuleHandleW("ntdll.dll");
            IntPtr version = ntdll == IntPtr.Zero ? IntPtr.Zero : GetProcAddress(ntdll, "wine_get_version");
            if (version == IntPtr.Zero)
                return;
            s.Wine = Marshal.PtrToStringAnsi(Marshal.GetDelegateForFunctionPointer<GetVersion>(version)());
            IntPtr host = GetProcAddress(ntdll, "wine_get_host_version");
            if (host == IntPtr.Zero)
                return;
            Marshal.GetDelegateForFunctionPointer<GetHostVersion>(host)(out IntPtr system, out IntPtr release);
            s.Host = (Marshal.PtrToStringAnsi(system) + " " + Marshal.PtrToStringAnsi(release)).Trim();
        }
        catch (Exception e)
        {
            BugReports.Log?.Warn("Couldn't ask Wine for its version: " + e.Message);
        }
    }
}
