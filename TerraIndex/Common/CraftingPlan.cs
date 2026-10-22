using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;

namespace TerraIndex.Common
{
	// One step of a crafting tree: an item, how many are needed and how many of those the player already has.
	// When some are missing and the item can be crafted, also the recipe used and its ingredients.
	public class PlanNode
	{
		public int Type;
		// Set when the recipe accepts a group ("Any Iron Bar"): any member of it counts as owned.
		public RecipeGroup Group;
		public int Needed;
		public int Owned;
		// Null when the item isn't crafted: either the player has enough, or it is a base material (no usable recipe).
		public Recipe Recipe;
		public int Crafts;
		public readonly List<PlanNode> Children = new();

		public string Name => Group?.GetText() ?? Lang.GetItemNameValue(Type);
	}

	// A base material of a plan (it can't be crafted, or the player already has it), added up across the whole tree.
	public record PlanMaterial(int Type, RecipeGroup Group, int Needed, int Owned)
	{
		public int Missing => Math.Max(0, Needed - Owned);
		public string Name => Group?.GetText() ?? Lang.GetItemNameValue(Type);
	}

	// Full crafting tree for one craft of a recipe, using what the player owns first.
	// Owned items are taken from a shared pool, so two branches never count the same stack twice.
	// Intermediate items use their first recipe that doesn't loop back (see LoopsBack):
	// items whose every recipe loops are treated as base materials.
	public class CraftingPlan
	{
		private const int MaxDepth = 40;

		public PlanNode Root { get; }
		// Missing ones first, then by name.
		public List<PlanMaterial> Materials { get; }
		public List<int> Stations { get; } = new();
		public bool Ready => Materials.All(material => material.Missing == 0);

		public CraftingPlan(int type, Recipe recipe, Dictionary<int, int> owned)
		{
			var pool = new Dictionary<int, int>(owned);
			// The goal itself is always crafted, even if the player already has one.
			Root = new PlanNode { Type = type, Needed = recipe.createItem.stack };
			Expand(Root, recipe, Root.Needed, pool, new HashSet<int> { type }, 0);

			var totals = new Dictionary<(int, RecipeGroup), PlanMaterial>();
			CollectMaterials(Root, totals);
			Materials = totals.Values.OrderBy(material => material.Missing == 0).ThenBy(material => material.Name).ToList();
		}

		// Items in the player's inventory, by type.
		public static Dictionary<int, int> Inventory(Player player)
		{
			var owned = new Dictionary<int, int>();
			foreach (Item item in player.inventory) {
				if (!item.IsAir)
					owned[item.type] = owned.GetValueOrDefault(item.type) + item.stack;
			}
			return owned;
		}

		private void Expand(PlanNode node, Recipe recipe, int missing, Dictionary<int, int> pool, HashSet<int> path, int depth)
		{
			node.Recipe = recipe;
			node.Crafts = (missing + recipe.createItem.stack - 1) / recipe.createItem.stack;
			foreach (int tile in recipe.requiredTile) {
				if (!Stations.Contains(tile))
					Stations.Add(tile);
			}
			foreach (Item ingredient in recipe.requiredItem) {
				RecipeGroup group = RecipeDatabase.GroupFor(recipe, ingredient.type);
				node.Children.Add(Plan(ingredient.type, group, ingredient.stack * node.Crafts, pool, path, depth + 1));
			}
		}

		private PlanNode Plan(int type, RecipeGroup group, int needed, Dictionary<int, int> pool, HashSet<int> path, int depth)
		{
			var node = new PlanNode { Type = type, Group = group, Needed = needed };
			node.Owned = Take(pool, type, group, needed);
			int missing = needed - node.Owned;
			if (missing == 0 || depth >= MaxDepth)
				return node;

			Recipe recipe = ChooseRecipe(type, path);
			if (recipe == null)
				return node;

			path.Add(type);
			Expand(node, recipe, missing, pool, path, depth);
			path.Remove(type);
			return node;
		}

		private static int Take(Dictionary<int, int> pool, int type, RecipeGroup group, int amount)
		{
			int taken = 0;
			foreach (int candidate in group != null ? group.ValidItems : new HashSet<int> { type }) {
				if (taken >= amount)
					break;
				if (!pool.TryGetValue(candidate, out int have) || have <= 0)
					continue;
				int used = Math.Min(have, amount - taken);
				pool[candidate] = have - used;
				taken += used;
			}
			return taken;
		}

		private static Recipe ChooseRecipe(int type, HashSet<int> path)
		{
			foreach (Recipe recipe in RecipeDatabase.RecipesFor(type)) {
				if (!recipe.requiredItem.Any(ingredient => LoopsBack(ingredient.type, type, path)))
					return recipe;
			}
			return null;
		}

		// True if crafting with this ingredient leads back to the item: it is the item itself, it is already being
		// crafted further up the tree, or it is made from the item (reversible recipes: Wood <-> Wood Platform,
		// blocks <-> walls). The last check matters because the path alone would accept Wood from Wood Platforms.
		private static bool LoopsBack(int ingredient, int type, HashSet<int> path) =>
			ingredient == type || path.Contains(ingredient)
			|| RecipeDatabase.RecipesFor(ingredient).Any(recipe => recipe.requiredItem.Any(item => item.type == type));

		private static void CollectMaterials(PlanNode node, Dictionary<(int, RecipeGroup), PlanMaterial> totals)
		{
			if (node.Recipe == null) {
				var key = (node.Type, node.Group);
				PlanMaterial total = totals.GetValueOrDefault(key) ?? new PlanMaterial(node.Type, node.Group, 0, 0);
				totals[key] = total with { Needed = total.Needed + node.Needed, Owned = total.Owned + node.Owned };
				return;
			}
			foreach (PlanNode child in node.Children)
				CollectMaterials(child, totals);
		}
	}
}
