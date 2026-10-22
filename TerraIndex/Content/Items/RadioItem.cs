using TerraIndex.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Items
{
	// Placed like a Piggy Bank (2x1); see RadioTile and RadioSystem for what it plays and when.
	public class RadioItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<RadioTile>());
			Item.width = 32;
			Item.height = 17;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.sellPrice(silver: 20);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.Wood, 8)
				.AddRecipeGroup(RecipeGroupID.IronBar, 3)
				.AddIngredient(ItemID.Glass, 2)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
