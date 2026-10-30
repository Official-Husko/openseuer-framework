using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Assets;
using ObenFramework.Building;

namespace DevBridge.Commands;

/// <summary>Custom furniture, flatpacks, blueprints and building areas, through ObenFramework.Building.</summary>
internal static class CustomFurnitureCommands
{
    private static readonly Dictionary<string, IDisposable> Loot = new Dictionary<string, IDisposable>();

    private static readonly EventRecorder Events = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        BuildingAreas.Entered.Subscribe(owner, a => recorder.Add("AreaEntered", Area(a)));
        BuildingAreas.Exited.Subscribe(owner, a => recorder.Add("AreaExited", Area(a)));
        CustomFurniture.Loaded.Subscribe(owner, p => recorder.Add("CustomLoaded", new JObject { ["id"] = p.furniture != null ? p.furniture.id : null, ["guid"] = p.GUID }));
    });

    [BridgeCommand("furniture.custom", "Custom furniture: the defined pieces and the ids that loaded as stand-ins. With 'key' and 'template' defines a test piece owned by the bridge.",
        Params = new[] { "key?: defines oben:DevBridge/<key>", "template?: game piece to copy, e.g. Red Metal Chair", "title?: its title", "details?: its description",
            "priceOC?: price", "category?: build menu group", "model?: a loaded model's full name (see assets.list)", "shop?: a furniture shop to sell it",
            "blueprint?: also a blueprint (1 scrap metal, 60 s) found in this pool: general, Start or a level's category" })]
    private static object Custom(JObject p)
    {
        string defined = null;
        string key = p.Str("key", null);
        if (key != null)
        {
            var definition = new FurnitureDefinition
            {
                Template = p.Str("template"), Title = p.Str("title", null), Details = p.Str("details", null), PriceOC = p.Int("priceOC", -1),
                Category = p.Str("category", null),
            };
            string model = p.Str("model", null);
            if (model != null)
                definition.Model = ModelRegistry.Find(model) ?? throw new ArgumentException($"No model '{model}' is loaded.");
            string shop = p.Str("shop", null);
            if (shop != null)
                definition.Shops.Add(shop);
            defined = CustomFurniture.Define(DevBridgeMod.Instance, key, definition);
            string pool = p.Str("blueprint", null);
            if (pool != null)
            {
                CustomFurniture.DefineBlueprint(DevBridgeMod.Instance, defined, new BlueprintDefinition
                {
                    Items = { [35180] = 1 }, Seconds = 60f, Pools = { pool == "general" ? CustomFurniture.GeneralPool : pool },
                });
            }
        }

        var list = new JArray();
        foreach (string id in CustomFurniture.Ids)
        {
            var built = CustomFurniture.Get(id);
            list.Add(new JObject
            {
                ["id"] = id, ["built"] = built != null, ["title"] = built != null ? built.title : null,
                ["category"] = built != null ? FurnitureCategories.NameOf(built.category) : null,
                ["details"] = built != null ? built.details : null, ["priceOC"] = built != null ? built.priceOC : (int?)null,
                ["standIn"] = CustomFurniture.IsStandIn(built), ["blueprint"] = CustomFurniture.BlueprintOf(id) != null,
                ["blueprintKnown"] = CustomFurniture.IsBlueprintKnown(id),
            });
        }
        return new JObject { ["defined"] = defined, ["available"] = CustomFurniture.IsAvailable, ["pieces"] = list, ["missing"] = new JArray(CustomFurniture.Missing) };
    }

    [BridgeCommand("furniture.give", "Gives a custom piece to the building inventory, or as flatpack items.",
        Params = new[] { "id: oben:mod/key", "amount?: default 1", "flatpack?: as flatpack items, default false" })]
    private static object Give(JObject p)
    {
        string id = p.Str("id");
        int amount = p.Int("amount", 1);
        bool ok = p.Bool("flatpack", false) ? CustomFurniture.GiveFlatpack(id, amount) : CustomFurniture.Give(id, amount);
        return new JObject { ["ok"] = ok };
    }

    [BridgeCommand("furniture.loot", "Adds a custom piece to a loot category as flatpacks (weight 1-100), or takes the bridge's entry out with clear.",
        Params = new[] { "id: oben:mod/key", "category: loot category name (see loot.pools)", "weight?: default 5", "clear?: remove instead" })]
    private static object AddLoot(JObject p)
    {
        string id = p.Str("id");
        string category = p.Str("category");
        string key = id + "|" + category;
        if (Loot.TryGetValue(key, out var old))
        {
            old.Dispose();
            Loot.Remove(key);
        }
        if (!p.Bool("clear", false))
            Loot[key] = CustomFurniture.AddFlatpackLoot(DevBridgeMod.Instance, id, category, p.Int("weight", 5));
        return new JObject { ["entries"] = new JArray(Loot.Keys) };
    }

    [BridgeCommand("furniture.blueprints", "Furniture blueprints of the level (game and mod), or gives one with 'give'.",
        Params = new[] { "text?: only titles containing this", "give?: id or title of a blueprint to give", "max?: default 60" })]
    private static object BlueprintList(JObject p)
    {
        string give = p.Str("give", null);
        bool? given = null;
        if (give != null)
        {
            var blueprint = Blueprints.Find(give);
            if (blueprint == null)
                throw new ArgumentException($"No blueprint '{give}'.");
            given = Blueprints.Give(blueprint);
        }
        string text = p.Str("text", null);
        int max = Math.Max(1, p.Int("max", 60));
        var list = new JArray();
        int total = 0;
        foreach (var info in Blueprints.All())
        {
            if (text != null && (info.Title == null || info.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0))
                continue;
            total++;
            if (list.Count < max)
                list.Add(new JObject { ["id"] = info.Id, ["title"] = info.Title, ["result"] = info.Result, ["known"] = info.Known, ["custom"] = info.Custom });
        }
        return new JObject { ["given"] = given, ["total"] = total, ["blueprints"] = list };
    }

    [BridgeCommand("building.area", "The building zone the player is in, and area events with events=true.", Params = new[] { "events?: return recorded events, default false" })]
    private static object AreaNow(JObject p)
    {
        var current = BuildingAreas.Current;
        var result = new JObject { ["area"] = current.HasValue ? Area(current.Value) : null };
        if (p.Bool("events", false))
            result["events"] = Events.Read(true);
        return result;
    }

    private static JObject Area(BuildingAreaInfo a) =>
        new JObject { ["id"] = a.Id, ["removeOnly"] = a.RemoveOnly, ["menuDisabled"] = a.MenuDisabled, ["notFullyExamined"] = a.NotFullyExamined };
}
