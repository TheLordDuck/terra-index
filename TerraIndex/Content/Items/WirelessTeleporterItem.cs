using TerraIndex.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Items
{
	// A teleporter that needs no wire; see WirelessTeleporterTile and TeleporterLinkerItem.
	public class WirelessTeleporterItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<WirelessTeleporterTile>());
			Item.width = 48;
			Item.height = 16;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.sellPrice(gold: 1);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Teleporter)
				.AddIngredient(ItemID.Wire, 10)
				.AddTile(TileID.TinkerersWorkbench)
				.Register();
		}
	}
}
