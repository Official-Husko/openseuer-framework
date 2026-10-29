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

    /// <summary>
    /// Starts from one of the game's recipes: its station, look in the crafting list, conditions, tags,
    /// variant and stolen-item rules, skill, time, fail chance and meta are copied, so the recipe behaves
    /// like its template. Inputs and outputs start empty; give them with <see cref="Input"/> and <see cref="Output"/>.
    /// </summary>
    public static RecipeBuilder From(Recipe template, int id, string name)
    {
        if (template == null)
            throw new System.ArgumentNullException(nameof(template));
        var source = template.Appearance;
        var appearance = new Recipe.RecipeAppearance();
        if (source != null)
        {
            appearance.SpritePath = source.SpritePath;
            appearance.Sprite = source.Sprite;
            appearance.CraftShowModel = source.CraftShowModel;
            appearance.itemToShow = source.itemToShow != null ? new ItemReference(source.itemToShow.ID) : null;
        }
        return new RecipeBuilder(new Recipe
        {
            ID = id,
            Name = name,
            Type = template.Type,
            Appearance = appearance,
            RecipeRequired = template.RecipeRequired,
            RequiredConditions = new List<RecipeCondition>(template.RequiredConditions ?? new List<RecipeCondition>()),
            ExcludingConditions = new List<RecipeCondition>(template.ExcludingConditions ?? new List<RecipeCondition>()),
            RecipeTags = new List<string>(template.RecipeTags ?? new List<string>()),
            variantItemCategory = template.variantItemCategory,
            variantItemAmount = template.variantItemAmount,
            useOnlyStolenItems = template.useOnlyStolenItems,
            allowedNonStolenItemsIds = new List<int>(template.allowedNonStolenItemsIds ?? new List<int>()),
            keepItemOwnerOnFail = template.keepItemOwnerOnFail,
            disableJunkSpawn = template.disableJunkSpawn,
            input = new Dictionary<ItemReference, float>(),
            output = new Dictionary<ItemReference, float>(),
            outputFail = new Dictionary<ItemReference, float>(),
            // The meta settings can name mail receivers and linked certificates of the template; start clean.
            metaSettings = new Recipe.RecipeMetaSettigns(),
            FailChance = template.FailChance,
            RelevantSkill = template.RelevantSkill,
            MinimumSkill = template.MinimumSkill,
            HardMinimumSkill = template.HardMinimumSkill,
            SkillFactor = template.SkillFactor,
            SkillGainFactor = template.SkillGainFactor,
            ProcessTime = template.ProcessTime,
            Meta = template.Meta != null ? (object[])template.Meta.Clone() : null,
        });
    }

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

    /// <summary>With <paramref name="hard"/>, players below the minimum skill can't craft it at all (otherwise they can, with a worse chance).</summary>
    public RecipeBuilder HardMinimumSkill(bool hard = true)
    {
        _recipe.HardMinimumSkill = hard;
        return this;
    }

    /// <summary>How much the skill level changes the fail chance, and how much skill a craft teaches (1 is the game's usual).</summary>
    public RecipeBuilder SkillFactors(float skill = 1f, float gain = 1f)
    {
        _recipe.SkillFactor = skill;
        _recipe.SkillGainFactor = gain;
        return this;
    }

    /// <summary>Recipe tags, which stations and recipe filters can look for.</summary>
    public RecipeBuilder Tags(params string[] tags)
    {
        _recipe.RecipeTags = new List<string>(tags ?? new string[0]);
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
