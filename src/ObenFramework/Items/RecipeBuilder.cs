using System.Collections.Generic;

namespace ObenFramework.Items;

/// <summary>
/// Builds a <see cref="Recipe"/> for <see cref="RecipeRegistry.Register"/> without touching the game's
/// serialized dictionaries by hand. Items are passed as objects, so a mod's own items work before the
/// recipe is added to the database.
/// </summary>
public sealed class RecipeBuilder
{
    private readonly Recipe _recipe;

    private RecipeBuilder(Recipe recipe) => _recipe = recipe;

    /// <param name="id">An id from <see cref="ReservedIds"/>.</param>
    /// <param name="name">The name shown in the crafting list.</param>
    /// <param name="type">Which kind of station crafts this (Basic is the hand-crafting list).</param>
    public static RecipeBuilder Create(int id, string name, RecipeType type) => new RecipeBuilder(new Recipe
    {
        ID = id,
        Name = name,
        Type = type,
        input = new Dictionary<ItemReference, float>(),
        output = new Dictionary<ItemReference, float>(),
        outputFail = new Dictionary<ItemReference, float>(),
        Appearance = new Recipe.RecipeAppearance(),
        metaSettings = new Recipe.RecipeMetaSettigns(),
    });

    public RecipeBuilder Input(Item item, float amount = 1f)
    {
        Put(_recipe.input, item, amount);
        return this;
    }

    public RecipeBuilder Output(Item item, float amount = 1f)
    {
        Put(_recipe.output, item, amount);
        return this;
    }

    /// <summary>What is made when the craft fails (the skill roll). Leave out for nothing.</summary>
    public RecipeBuilder OutputOnFail(Item item, float amount = 1f)
    {
        Put(_recipe.outputFail, item, amount);
        return this;
    }

    /// <summary>Seconds of game time the craft takes. 0 is instant.</summary>
    public RecipeBuilder Time(int seconds)
    {
        _recipe.ProcessTime = seconds;
        return this;
    }

    /// <summary>Chance from 0 to 1 that the craft fails, before the player's skill is applied.</summary>
    public RecipeBuilder FailChance(float chance)
    {
        _recipe.FailChance = chance;
        return this;
    }

    public RecipeBuilder Skill(PlayerStats.PlayerSkillType skill, int minimum = 0)
    {
        _recipe.RelevantSkill = skill;
        _recipe.MinimumSkill = minimum;
        return this;
    }

    /// <summary>Hide the recipe until the player learns it.</summary>
    public RecipeBuilder RequiresLearning(bool required = true)
    {
        _recipe.RecipeRequired = required;
        return this;
    }

    // The game builds a dictionary keyed by item from these, which throws on a repeated item, so merge repeats.
    private static void Put(Dictionary<ItemReference, float> items, Item item, float amount)
    {
        foreach (var existing in new List<ItemReference>(items.Keys))
        {
            if (existing.ID == item.ID)
            {
                items[existing] += amount;
                return;
            }
        }
        items[new ItemReference(item)] = amount;
    }

    /// <summary>The recipe as built. Edit anything the builder doesn't cover on it before registering.</summary>
    public Recipe Build() => _recipe;
}
