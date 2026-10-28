// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.Crafting;

/// <summary>A snapshot of one recipe, with what it takes and makes.</summary>
public sealed class RecipeInfo
{
    internal RecipeInfo(Recipe recipe, bool known)
    {
        Recipe = recipe;
        Id = recipe.ID;
        Name = recipe.Name;
        Type = recipe.Type.ToString();
        ProcessSeconds = recipe.ProcessTime;
        Skill = recipe.RelevantSkill.ToString();
        MinimumSkill = recipe.MinimumSkill;
        HardMinimumSkill = recipe.HardMinimumSkill;
        FailChance = recipe.FailChance;
        RequiresLearning = recipe.RecipeRequired;
        Known = known;
        Ingredients = ItemCount.From(recipe.input);
        Results = ItemCount.From(recipe.output);
        FailResults = ItemCount.From(recipe.outputFail);
        RequiredConditions = CraftingStations.Names(recipe.RequiredConditions);
        ExcludingConditions = CraftingStations.Names(recipe.ExcludingConditions);
        Tags = recipe.RecipeTags != null ? new List<string>(recipe.RecipeTags) : new List<string>();
    }

    /// <summary>The game's recipe object. Don't change it: the game shares it between all stations.</summary>
    public Recipe Recipe { get; }

    public int Id { get; }

    public string Name { get; }

    /// <summary>The <c>RecipeType</c> name: which stations list it.</summary>
    public string Type { get; }

    /// <summary>Base crafting time in game seconds, before equipment and speed.</summary>
    public int ProcessSeconds { get; }

    /// <summary>The skill it trains and depends on (<c>PlayerSkillType</c> name).</summary>
    public string Skill { get; }

    public int MinimumSkill { get; }

    /// <summary>The minimum skill is required, not only advised.</summary>
    public bool HardMinimumSkill { get; }

    public float FailChance { get; }

    /// <summary>Hidden until the player has found or learned it.</summary>
    public bool RequiresLearning { get; }

    /// <summary>The player has found or learned it.</summary>
    public bool Known { get; }

    public List<ItemCount> Ingredients { get; }

    public List<ItemCount> Results { get; }

    public List<ItemCount> FailResults { get; }

    /// <summary>Conditions (<c>RecipeCondition</c> names) the station must provide.</summary>
    public List<string> RequiredConditions { get; }

    /// <summary>Conditions that rule the station out.</summary>
    public List<string> ExcludingConditions { get; }

    public List<string> Tags { get; }
}

/// <summary>A recipe became known to the player, raised by <see cref="Recipes.Unlocked"/>.</summary>
public readonly struct RecipeUnlock
{
    public RecipeUnlock(int recipeId, string name, bool learned)
    {
        RecipeId = recipeId;
        Name = name;
        Learned = learned;
    }

    public int RecipeId { get; }

    public string Name { get; }

    /// <summary>True when the player learned it by crafting it, false when it was found (a note, a book, a mod).</summary>
    public bool Learned { get; }
}
