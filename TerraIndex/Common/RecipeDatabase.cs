using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;

namespace TerraIndex.Common
{
	// Index of every recipe (vanilla and modded), built once the game has finished loading them.
	public class RecipeDatabase : ModSystem
	{
		private static readonly List<Recipe> Empty = new();
		private static readonly Dictionary<int, List<Recipe>> byResult = new();
		private static readonly Dictionary<int, List<Recipe>> byIngredient = new();
		private static readonly Dictionary<int, int> tileToItem = new();
		// Every tile some recipe asks for.
		private static readonly HashSet<int> stationTiles = new();

		// Vanilla stations that also count as others (as in Player.AdjTiles). Modded ones use ModTile.AdjTiles.
		private static readonly Dictionary<int, int[]> vanillaAdjTiles = new() {
			[TileID.Hellforge] = new int[] { TileID.Furnaces },
			[TileID.AdamantiteForge] = new int[] { TileID.Furnaces, TileID.Hellforge },
			[TileID.MythrilAnvil] = new int[] { TileID.Anvils },
			[TileID.AlchemyTable] = new int[] { TileID.Bottles, TileID.Tables },
			[TileID.BewitchingTable] = new int[] { TileID.Tables },
		};

		public static List<int> AllItems { get; } = new();

		public override void PostSetupRecipes() => Build();

		public override void Unload() => Clear();

		private static void Build()
		{
			Clear();

			for (int i = 0; i < Recipe.numRecipes; i++) {
				Recipe recipe = Main.recipe[i];
				if (recipe == null || recipe.Disabled || recipe.createItem.IsAir)
					continue;

				Add(byResult, recipe.createItem.type, recipe);
				stationTiles.UnionWith(recipe.requiredTile);

				// An item counts as an ingredient if it is listed directly or belongs to an accepted group ("Any Wood", etc.)
				var seen = new HashSet<int>();
				foreach (Item item in recipe.requiredItem) {
					if (seen.Add(item.type))
						Add(byIngredient, item.type, recipe);
				}
				foreach (int groupId in recipe.acceptedGroups) {
					if (!RecipeGroup.recipeGroups.TryGetValue(groupId, out RecipeGroup group))
						continue;
					foreach (int type in group.ValidItems) {
						if (seen.Add(type))
							Add(byIngredient, type, recipe);
					}
				}
			}

			for (int type = 1; type < ItemLoader.ItemCount; type++) {
				if (ItemID.Sets.Deprecated[type])
					continue;
				AllItems.Add(type);

				int tile = ContentSamples.ItemsByType[type].createTile;
				if (tile >= 0)
					tileToItem.TryAdd(tile, type);
			}
		}

		private static void Clear()
		{
			byResult.Clear();
			byIngredient.Clear();
			tileToItem.Clear();
			stationTiles.Clear();
			AllItems.Clear();
			CraftingPlan.ClearCache();
		}

		// Whether this tile is worth reporting as a crafting station found on a terminal's wire.
		public static bool IsStation(int tile) =>
			stationTiles.Contains(tile) || vanillaAdjTiles.ContainsKey(tile) || TileLoader.GetTile(tile)?.AdjTiles.Length > 0;

		// The stations a tile counts as: itself, plus e.g. Furnace for a Hellforge.
		public static IEnumerable<int> Provides(int tile)
		{
			yield return tile;
			if (vanillaAdjTiles.TryGetValue(tile, out int[] adjacent)) {
				foreach (int other in adjacent)
					yield return other;
			}
			ModTile modTile = TileLoader.GetTile(tile);
			if (modTile != null) {
				foreach (int other in modTile.AdjTiles)
					yield return other;
			}
		}

		private static void Add(Dictionary<int, List<Recipe>> map, int key, Recipe recipe)
		{
			if (!map.TryGetValue(key, out List<Recipe> list))
				map[key] = list = new List<Recipe>();
			list.Add(recipe);
		}

		public static List<Recipe> RecipesFor(int type) => byResult.TryGetValue(type, out var list) ? list : Empty;

		public static List<Recipe> UsesOf(int type) => byIngredient.TryGetValue(type, out var list) ? list : Empty;

		// Item that places this crafting station (used to draw its icon), or 0 if there is none.
		public static int ItemForTile(int tile) => tileToItem.TryGetValue(tile, out int type) ? type : 0;

		public static string TileName(int tile)
		{
			string name = Lang.GetMapObjectName(MapHelper.TileToLookup(tile, 0));
			if (!string.IsNullOrWhiteSpace(name))
				return name;

			int item = ItemForTile(tile);
			if (item > 0)
				return Lang.GetItemNameValue(item);

			return tile < TileID.Count ? TileID.Search.GetName(tile) : TileLoader.GetTile(tile)?.Name ?? tile.ToString();
		}

		// Items accepted for one of the recipe's ingredients: the listed one first, then the rest of its group, if any.
		public static IEnumerable<int> AcceptedItems(Recipe recipe, int ingredient)
		{
			yield return ingredient;
			RecipeGroup group = GroupFor(recipe, ingredient);
			if (group == null)
				yield break;
			foreach (int type in group.ValidItems) {
				if (type != ingredient)
					yield return type;
			}
		}

		// Recipe group ("Any Iron Bar", ...) that accepts this ingredient, or null.
		public static RecipeGroup GroupFor(Recipe recipe, int type)
		{
			foreach (int groupId in recipe.acceptedGroups) {
				if (RecipeGroup.recipeGroups.TryGetValue(groupId, out RecipeGroup group) && group.ValidItems.Contains(type))
					return group;
			}
			return null;
		}
	}
}
