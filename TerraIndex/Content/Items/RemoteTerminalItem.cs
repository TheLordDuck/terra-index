using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using TerraIndex.Common;
using TerraIndex.Content.Storage;
using TerraIndex.Content.Tiles;
using TerraIndex.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TerraIndex.Content.Items
{
	// Opens a terminal from anywhere. It is linked by right clicking a terminal while holding it (see TerminalTile.RightClick).
	// The link is stored in the item together with the world's ID, so it is saved, synced to the server
	// (which uses it to allow requests from far away) and ignored in other worlds.
	public class RemoteTerminalItem : ModItem
	{
		// Without a linked portable terminal, players must be this close to use a terminal: the panel's reach
		// plus some margin, since the server sees the player a little behind.
		private static float NearDistance => 16 * (ServerConfig.Instance.TerminalReach + 10);

		public Point Link = new(-1, -1);
		public int LinkWorld;

		public bool IsLinked => Link.X >= 0 && LinkWorld == Main.worldID;

		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 28;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = Item.useAnimation = 20;
			Item.rare = ItemRarityID.Red;
			Item.value = Item.sellPrice(gold: 20);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<TerminalItem>()
				.AddIngredient(ItemID.LunarBar, 10)
				.AddTile(TileID.LunarCraftingStation)
				.Register();
		}

		public void LinkTo(Point terminal)
		{
			Link = terminal;
			LinkWorld = Main.worldID;
		}

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI != Main.myPlayer)
				return true;
			if (!ServerConfig.Instance.AllowPortableTerminal)
				Main.NewText(UIHelpers.T("RemoteDisabled"), Color.Orange);
			else if (IsLinked)
				ModContent.GetInstance<TerminalSystem>().Open(Link, true);
			else
				Main.NewText(UIHelpers.T("RemoteNotLinked"), Color.Orange);
			return true;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			string text = IsLinked
				? UIHelpers.T("RemoteLinkedTo", (int)(Main.LocalPlayer.Distance(TerminalTile.Center(Link)) / 16))
				: UIHelpers.T("RemoteNotLinked");
			tooltips.Add(new TooltipLine(Mod, "Link", text) { OverrideColor = IsLinked ? Color.LightGreen : Color.Orange });
		}

		// True if the player is carrying a portable terminal linked to this terminal (and the server allows them).
		public static bool IsLinkedTo(Player player, Point terminal)
		{
			if (!ServerConfig.Instance.AllowPortableTerminal)
				return false;
			foreach (Item item in player.inventory) {
				if (item.ModItem is RemoteTerminalItem remote && remote.IsLinked && remote.Link == terminal)
					return true;
			}
			return false;
		}

		// Whether the player may use this terminal: standing near it, or carrying a portable terminal linked to it.
		public static bool CanReach(Player player, Point terminal) =>
			player.Distance(TerminalTile.Center(terminal)) <= NearDistance || IsLinkedTo(player, terminal);

		public override void SaveData(TagCompound tag)
		{
			if (Link.X < 0)
				return;
			tag["x"] = Link.X;
			tag["y"] = Link.Y;
			tag["world"] = LinkWorld;
		}

		public override void LoadData(TagCompound tag)
		{
			if (!tag.ContainsKey("x"))
				return;
			Link = new Point(tag.GetInt("x"), tag.GetInt("y"));
			LinkWorld = tag.GetInt("world");
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(Link.X);
			writer.Write(Link.Y);
			writer.Write(LinkWorld);
		}

		public override void NetReceive(BinaryReader reader)
		{
			Link = new Point(reader.ReadInt32(), reader.ReadInt32());
			LinkWorld = reader.ReadInt32();
		}
	}

	// For testing: the Merchant sells it for 4 silver, so it can be tried without reaching the Moon Lord.
	// Take this out before publishing 1.0.
	public class MerchantTestShop : GlobalNPC
	{
		public override void ModifyShop(NPCShop shop)
		{
			if (shop.NpcType == NPCID.Merchant)
				shop.Add(new Item(ModContent.ItemType<RemoteTerminalItem>()) { shopCustomPrice = Item.buyPrice(silver: 4) });
		}
	}
}
