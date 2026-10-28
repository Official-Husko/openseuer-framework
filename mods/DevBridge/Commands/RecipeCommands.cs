using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Crafting;

namespace DevBridge.Commands;

/// <summary>Finding recipes and giving them to the player.</summary>
internal static class RecipeCommands
{
    [BridgeCommand("recipes.find", "Recipes by id, by part of the name, or the ones making or using an item.",
        Params = new[] { "id?: recipe id", "text?: part of the name", "makes?: item id in the output", "uses?: item id in the input", "max?: default 20" })]
    private static object Find(JObject p)
    {
        int max = Math.Max(1, p.Int("max", 20));
        List<RecipeInfo> found;
        if (p["id"] != null)
            found = Recipes.Get(p.Int("id", -1)) is RecipeInfo one ? new List<RecipeInfo> { one } : new List<RecipeInfo>();
        else if (p["makes"] != null)
            found = Recipes.Making(p.Int("makes", -1));
        else if (p["uses"] != null)
            found = Recipes.Using(p.Int("uses", -1));
        else
            found = Recipes.Search(p.Str("text", ""));

        var list = new JArray();
        for (int i = 0; i < found.Count && i < max; i++)
        {
            var r = found[i];
            list.Add(new JObject
            {
                ["id"] = r.Id,
                ["name"] = r.Name,
                ["type"] = r.Type,
                ["seconds"] = r.ProcessSeconds,
                ["skill"] = r.Skill,
                ["minimumSkill"] = r.MinimumSkill,
                ["known"] = r.Known,
                ["requiresLearning"] = r.RequiresLearning,
                ["canCraftFromInventory"] = Recipes.CanCraftFromInventory(r.Id),
                ["ingredients"] = ItemJson.Counts(r.Ingredients),
                ["results"] = ItemJson.Counts(r.Results),
                ["conditions"] = new JArray(r.RequiredConditions),
            });
        }
        return new JObject { ["total"] = found.Count, ["recipes"] = list };
    }

    [BridgeCommand("recipes.unlock", "Give the player a recipe as if they found it.",
        Params = new[] { "id: recipe id", "notify?: show the game's notification, default true" })]
    private static object Unlock(JObject p)
    {
        int id = p.Int("id", -1);
        if (Recipes.ById(id) == null)
            throw new ArgumentException($"No recipe {id}. Try recipes.find.");
        bool added = Recipes.Unlock(id, p.Bool("notify", true));
        return new JObject { ["added"] = added, ["known"] = Recipes.Get(id) is RecipeInfo r && r.Known };
    }
}
