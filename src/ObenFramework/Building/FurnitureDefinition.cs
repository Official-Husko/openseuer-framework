using System.Collections.Generic;
using ObenFramework.Assets;
using UnityEngine;

namespace ObenFramework.Building;

/// <summary>Where a custom piece may be placed, if not where its template goes.</summary>
public enum FurniturePlacement
{
    /// <summary>Wherever the template piece goes.</summary>
    Template,
    Floor,
    Wall,
    Ceiling,
    FloorAndWall,
    Any,
}

/// <summary>
/// A new piece of furniture, made as a copy of one of the game's pieces (the template) with its own name,
/// prices, icon and optionally its own model. It behaves like the template: a chair copy can be sat on, a
/// shelf copy stores things.
/// </summary>
public sealed class FurnitureDefinition
{
    /// <summary>
    /// The game piece to copy: its short name as the build menu and the <c>add_furniture</c> console command
    /// know it ("Red Metal Chair"), its asset path, or its title.
    /// </summary>
    public string Template;

    /// <summary>Name in the build menu, shops and inventory. Null keeps the template's.</summary>
    public string Title;

    /// <summary>Description. Null keeps the template's.</summary>
    public string Details;

    /// <summary>Price in Open Sewer coins, or -1 for the template's.</summary>
    public int PriceOC = -1;

    /// <summary>Price in real money, or -1 for the template's.</summary>
    public int PriceRM = -1;

    /// <summary>Build menu and inventory icon. Null keeps the template's.</summary>
    public Sprite Icon;

    /// <summary>
    /// A model from <c>ModelRegistry</c> to show instead of the template's mesh, or null to keep the template's
    /// look. It is fitted onto the template: centred, standing on the template's base, at its own size. The
    /// framework keeps its own copy, so the piece stays intact after the model's mod unloads.
    /// </summary>
    public ModelTemplate Model;

    /// <summary>
    /// Solid boxes in the model's space replacing the template's colliders, or null for one box around the
    /// model. Only used with <see cref="Model"/>; at most 64.
    /// </summary>
    public Bounds[] Colliders;

    public FurniturePlacement Placement = FurniturePlacement.Template;

    /// <summary>
    /// The build menu group: one of the game's (Chairs, Tables, ...) or a new one by name. Null keeps the
    /// template's.
    /// </summary>
    public string Category;

    /// <summary>Furniture shops that sell it, by name or GUID (see <c>Shops</c>). Empty: none.</summary>
    public List<string> Shops = new List<string>();

    /// <summary>How many a shop has after each restock.</summary>
    public int ShopStock = 3;
}

/// <summary>A blueprint for a custom piece: what crafting it takes and where players can find it.</summary>
public sealed class BlueprintDefinition
{
    /// <summary>Name of the blueprint. Null gives "&lt;furniture title&gt; Blueprint".</summary>
    public string Title;

    public string Details;

    /// <summary>Icon of the blueprint. Null uses the furniture's.</summary>
    public Sprite Icon;

    /// <summary>Item ingredients: item id to amount.</summary>
    public Dictionary<int, int> Items = new Dictionary<int, int>();

    /// <summary>Furniture ingredients, one of each: game pieces by name or path, mod pieces by id.</summary>
    public List<string> Furniture = new List<string>();

    /// <summary>Crafting time in game seconds.</summary>
    public float Seconds = 3600f;

    /// <summary>The skill it trains and needs, or None.</summary>
    public PlayerStats.PlayerSkillType Skill = PlayerStats.PlayerSkillType.None;

    public int MinimumSkill;

    /// <summary>How many pieces one craft makes.</summary>
    public int Amount = 1;

    /// <summary>
    /// Where players can find it as loot: "" for the general pool, "Start" for the first finds, or a blueprint
    /// category of the level. Empty: only through code (<c>CustomFurniture.GiveBlueprint</c>) or mail.
    /// </summary>
    public List<string> Pools = new List<string>();
}
