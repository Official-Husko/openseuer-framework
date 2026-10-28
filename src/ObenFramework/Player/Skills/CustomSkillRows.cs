// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using TMPro;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// Lists learned custom skills (level 1 or more) on the character page under the game's skills. The
/// rows are copies of the game's own row prefab, filled after the game refreshes its list, so they look
/// and update the same. They are destroyed when the framework unloads.
/// </summary>
[HarmonyPatch(typeof(CharacterPanel), nameof(CharacterPanel.UpdateSkills))]
internal static class CustomSkillRows
{
    private const string RowPrefix = "ModSkill:";

    private static readonly AccessTools.FieldRef<CharacterPanel, GameObject> PanelField =
        AccessTools.FieldRefAccess<CharacterPanel, GameObject>("skillPanel");
    private static readonly AccessTools.FieldRef<CharacterPanel, Transform> ListField =
        AccessTools.FieldRefAccess<CharacterPanel, Transform>("skillList");
    private static readonly AccessTools.FieldRef<CharacterPanel, Transform> PrefabField =
        AccessTools.FieldRefAccess<CharacterPanel, Transform>("skillPrefab");
    private static readonly AccessTools.FieldRef<CharacterPanel, TextMeshProUGUI> EmptyTextField =
        AccessTools.FieldRefAccess<CharacterPanel, TextMeshProUGUI>("emptyPanelText");
    private static readonly AccessTools.FieldRef<CharacterPanelSkill, TextMeshProUGUI> TitleField =
        AccessTools.FieldRefAccess<CharacterPanelSkill, TextMeshProUGUI>("title");
    private static readonly AccessTools.FieldRef<CharacterPanelSkill, TextMeshProUGUI> LevelField =
        AccessTools.FieldRefAccess<CharacterPanelSkill, TextMeshProUGUI>("level");
    private static readonly AccessTools.FieldRef<CharacterPanelSkill, TextMeshProUGUI> XpField =
        AccessTools.FieldRefAccess<CharacterPanelSkill, TextMeshProUGUI>("xp");

    private static readonly List<GameObject> Rows = new List<GameObject>();

    public static void Init(ObenMod framework) => framework.Track(Disposable.Create(() =>
    {
        foreach (var row in Rows)
        {
            if (row != null)
                UnityEngine.Object.Destroy(row);
        }
        Rows.Clear();
    }));

    [HarmonyPostfix]
    private static void AfterUpdateSkills(CharacterPanel __instance)
    {
        try
        {
            Refresh(__instance);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Listing custom skills on the character page failed", e);
        }
    }

    private static void Refresh(CharacterPanel panel)
    {
        var list = ListField(panel);
        var prefab = PrefabField(panel);
        var skillPanel = PanelField(panel);
        if (list == null || prefab == null || skillPanel == null)
            return;

        Rows.RemoveAll(r => r == null);
        bool any = false;
        foreach (var skill in CustomSkills.All)
        {
            var row = FindRow(list, RowPrefix + skill.Key);
            bool show = skill.ShowInCharacterPanel && skill.Level >= 1;
            if (!show)
            {
                if (row != null)
                    row.gameObject.SetActive(false);
                continue;
            }
            if (row == null)
                row = NewRow(prefab, list, RowPrefix + skill.Key);
            Fill(row, skill);
            any = true;
        }

        // Rows of skills whose mod has gone.
        for (int i = 0; i < list.childCount; i++)
        {
            var child = list.GetChild(i);
            if (child.name.StartsWith(RowPrefix, StringComparison.Ordinal) && !IsListed(child.name))
                child.gameObject.SetActive(false);
        }

        if (any)
        {
            skillPanel.SetActive(true);
            var empty = EmptyTextField(panel);
            if (empty != null && empty.text == "No learned skills")
                empty.text = "";
        }
    }

    private static Transform NewRow(Transform prefab, Transform list, string name)
    {
        var row = UnityEngine.Object.Instantiate(prefab, list);
        row.name = name;
        Rows.Add(row.gameObject);
        return row;
    }

    private static void Fill(Transform row, CustomSkill skill)
    {
        var view = row.GetComponent<CharacterPanelSkill>();
        if (view == null)
            return;

        // The game's row draws its bars on a 0..10 scale; a skill with another maximum is scaled onto it.
        float value = skill.Value;
        float scaled = value * PlayerStats.maxLevel / skill.Max;
        view.UpdateSkill(new PlayerStats.PlayerSkill { Type = PlayerStats.PlayerSkillType.None, Skill = scaled });

        var title = TitleField(view);
        if (title != null)
            title.text = skill.DisplayName;
        var level = LevelField(view);
        if (level != null)
            level.text = $"LVL {CustomSkillMath.Level(value)}";
        var xp = XpField(view);
        if (xp != null)
            xp.text = $"{Mathf.FloorToInt(value * 1000f)} XP";
        row.gameObject.SetActive(true);
    }

    private static Transform FindRow(Transform list, string name)
    {
        for (int i = 0; i < list.childCount; i++)
        {
            var child = list.GetChild(i);
            if (child.name == name)
                return child;
        }
        return null;
    }

    private static bool IsListed(string rowName)
    {
        foreach (var skill in CustomSkills.All)
        {
            if (rowName == RowPrefix + skill.Key)
                return true;
        }
        return false;
    }
}
