using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Items
{
	// Sets wireless teleporters' channels: hold it and right click one to open its channel menu (see
	// WirelessTeleporterTile.RightClick and TeleporterChannelState).
	public class TeleporterLinkerItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 24;
			Item.rare = ItemRarityID.Orange;
			Item.value = Terraria.Item.sellPrice(silver: 50);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wrench)
				.AddRecipeGroup(RecipeGroupID.IronBar, 3)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
