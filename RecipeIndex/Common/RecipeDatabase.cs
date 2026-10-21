using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;

namespace RecipeIndex.Common
{
	// Índice de todas las recetas (vanilla y de mods), construido una vez que el juego termina de cargarlas.
	public class RecipeDatabase : ModSystem
	{
		private static readonly List<Recipe> Empty = new();
		private static readonly Dictionary<int, List<Recipe>> byResult = new();
		private static readonly Dictionary<int, List<Recipe>> byIngredient = new();
		private static readonly Dictionary<int, int> tileToItem = new();

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

				// Un objeto cuenta como ingrediente si aparece directamente o si pertenece a un grupo aceptado ("Cualquier madera", etc.)
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
			AllItems.Clear();
		}

		private static void Add(Dictionary<int, List<Recipe>> map, int key, Recipe recipe)
		{
			if (!map.TryGetValue(key, out List<Recipe> list))
				map[key] = list = new List<Recipe>();
			list.Add(recipe);
		}

		public static List<Recipe> RecipesFor(int type) => byResult.TryGetValue(type, out var list) ? list : Empty;

		public static List<Recipe> UsesOf(int type) => byIngredient.TryGetValue(type, out var list) ? list : Empty;

		// Objeto que coloca esta estación de trabajo (para dibujar su icono), o 0 si no hay ninguno.
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

		// Grupo de receta ("Cualquier hierro", ...) que acepta este ingrediente, o null.
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
