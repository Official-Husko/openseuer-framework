// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections;
using HarmonyLib;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// Shows custom skill banners in the game's skill banner, one after another with the game's own
/// (see <see cref="SkillBannerQueue"/> for the order). A slow coroutine hands the next banner over
/// when the spot is free; the game's two banner calls are patched to wait in line behind mod banners.
/// </summary>
[HarmonyPatch]
internal static class SkillBanners
{
    private const float CheckSeconds = 0.25f;

    private static readonly SkillBannerQueue Queue = new SkillBannerQueue();

    private static readonly Action<PlayerSkillIncreaseUI, string> ShowNotification =
        AccessTools.MethodDelegate<Action<PlayerSkillIncreaseUI, string>>(
            AccessTools.Method(typeof(PlayerSkillIncreaseUI), "ShowNotification", new[] { typeof(string) }));

    public static void Init(ObenMod framework)
    {
        framework.StartCoroutine(Pump());
        GameEvents.SceneLoaded.Subscribe(framework, _ => Queue.Clear());
        framework.Track(Disposable.Create(Queue.Clear));
    }

    public static void AddSkill(string key, string text) => Queue.AddSkill(key, text);

    public static bool AddText(string text) => Queue.AddText(text);

    public static void Clear() => Queue.Clear();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerSkillIncreaseUI), nameof(PlayerSkillIncreaseUI.ShowSkillIncrease))]
    private static bool BeforeLevelUp(PlayerStats.PlayerSkillType skillType, int amount) =>
        AllowGame(CustomSkillMath.LevelUpText(skillType.ToString(), amount));

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerSkillIncreaseUI), nameof(PlayerSkillIncreaseUI.ShowNewSkill))]
    private static bool BeforeNewSkill(PlayerStats.PlayerSkillType skillType) =>
        AllowGame(CustomSkillMath.LearnedText(skillType.ToString()));

    private static bool AllowGame(string text)
    {
        try
        {
            return Queue.ClaimForGame(Time.unscaledTime, text);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Queueing a skill banner failed", e);
            return true;
        }
    }

    private static IEnumerator Pump()
    {
        var wait = new WaitForSecondsRealtime(CheckSeconds);
        while (true)
        {
            yield return wait;
            if (Queue.Count == 0)
                continue;
            var ui = PlayerSkillIncreaseUI.instance;
            if (ui == null || !Queue.Next(Time.unscaledTime, out string text))
                continue;
            try
            {
                ShowNotification(ui, text);
            }
            catch (Exception e)
            {
                GameEvents.Log.Error("Showing a skill banner failed", e);
            }
        }
    }
}
