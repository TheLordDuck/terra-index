using RecipeIndex.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace RecipeIndex.Content.Items
{
	// No recipe: the Mechanic sells it (see MechanicShop).
	public class TerminalItem : ModItem
	{
		public override void SetDefaults()
		{
			Item.DefaultToPlaceableTile(ModContent.TileType<TerminalTile>());
			Item.width = 24;
			Item.height = 24;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.buyPrice(silver: 5);
		}
	}

	public class MechanicShop : GlobalNPC
	{
		public override void ModifyShop(NPCShop shop)
		{
			if (shop.NpcType == NPCID.Mechanic)
				shop.Add<TerminalItem>();
		}
	}
}
