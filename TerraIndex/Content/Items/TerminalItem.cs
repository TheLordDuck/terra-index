using TerraIndex.Common;
using TerraIndex.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Items
{
	// No recipe: the Steampunker sells it (see SteampunkerShop), for the price set in ServerConfig.
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

		// The price comes from the server config, applied every time the shop opens so changes need no reload.
		public override void ModifyActiveShop(NPC npc, string shopName, Item[] items)
		{
			foreach (Item item in items) {
				if (item != null && item.type == ModContent.ItemType<TerminalItem>())
					item.shopCustomPrice = Item.buyPrice(gold: ServerConfig.Instance.TerminalPriceGold);
			}
		}
	}
}
