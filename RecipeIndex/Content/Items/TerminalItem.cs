using RecipeIndex.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace RecipeIndex.Content.Items
{
	// No recipe: the Steampunker sells it (see SteampunkerShop).
	public class TerminalItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<TerminalTile>());
			Item.width = 24;
			Item.height = 12;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.buyPrice(platinum: 1);
		}
	}

	public class SteampunkerShop : GlobalNPC
	{
		public override void ModifyShop(NPCShop shop)
		{
			if (shop.NpcType == NPCID.Steampunker)
				shop.Add<TerminalItem>();
		}
	}
}
