using Microsoft.Xna.Framework;
using TerraIndex.Content.Items;
using TerraIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TerraIndex.Content.Tiles
{
	// 3x1 teleporter that needs no wire: the Teleporter Linker puts it on a channel (see TeleporterSystem), and right
	// clicking it takes you to the next one on that channel. You can stand on it, like on a platform.
	public class WirelessTeleporterTile : ModTile
	{
		public const int Width = 3;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileSolidTop[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
			TileObjectData.newTile.Height = 1;
			TileObjectData.newTile.CoordinateHeights = new[] { 16 };
			TileObjectData.newTile.Origin = new Point16(1, 0);
			TileObjectData.newTile.LavaDeath = false;
			TileObjectData.addTile(Type);

			AddMapEntry(new Color(80, 220, 255), CreateMapEntryName());
			DustType = DustID.Electric;
		}

		public static Point TopLeft(int i, int j) => new(i - Main.tile[i, j].TileFrameX / 18, j);

		public static bool IsTeleporter(Point topLeft)
		{
			if (!WorldGen.InWorld(topLeft.X, topLeft.Y))
				return false;
			Tile tile = Main.tile[topLeft.X, topLeft.Y];
			return tile.HasTile && tile.TileType == ModContent.TileType<WirelessTeleporterTile>() && tile.TileFrameX == 0;
		}

		public static Vector2 Center(Point topLeft) => new(topLeft.X * 16 + Width * 8, topLeft.Y * 16 + 8);

		private static TeleporterSystem Links => ModContent.GetInstance<TeleporterSystem>();

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			if (Main.netMode != NetmodeID.MultiplayerClient)
				Links.Remove(new Point(i, j));
		}

		// Teleporters with somewhere to go glow.
		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			if (Links.TryGetDestination(TopLeft(i, j), out _)) {
				r = 0.1f;
				g = 0.45f;
				b = 0.6f;
			}
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = player.HeldItem.ModItem is TeleporterLinkerItem ? ModContent.ItemType<TeleporterLinkerItem>() : ModContent.ItemType<WirelessTeleporterItem>();
			int channel = Links.ChannelOf(TopLeft(i, j));
			player.cursorItemIconText = channel == TeleporterSystem.NoChannel ? UIHelpers.T("TeleporterNoChannel") : UIHelpers.T("TeleporterChannel", channel);
		}

		public override bool RightClick(int i, int j)
		{
			Player player = Main.LocalPlayer;
			Point here = TopLeft(i, j);
			if (player.HeldItem.ModItem is TeleporterLinkerItem) {
				Links.OpenMenu(here);
				return true;
			}

			// In multiplayer the destination may be too far away for this client to have its tiles; the server takes
			// broken teleporters off their channel, so the channel alone is trusted there.
			if (!Links.TryGetDestination(here, out Point destination) || (Main.netMode != NetmodeID.MultiplayerClient && !IsTeleporter(destination))) {
				int channel = Links.ChannelOf(here);
				Main.NewText(channel == TeleporterSystem.NoChannel ? UIHelpers.T("TeleporterNotLinked") : UIHelpers.T("TeleporterAlone", channel), Color.Orange);
				return true;
			}
			Teleport(player, destination);
			return true;
		}

		// Stands the player on top of the other teleporter, with the vanilla teleporter's effect.
		private static void Teleport(Player player, Point destination)
		{
			var position = new Vector2(destination.X * 16 + Width * 8 - player.width / 2f, destination.Y * 16 - player.height);
			player.Teleport(position, TeleportationStyleID.TeleporterTile);
			player.velocity = Vector2.Zero;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, player.whoAmI, position.X, position.Y, TeleportationStyleID.TeleporterTile);
			SoundEngine.PlaySound(SoundID.Item6, player.Center);
		}
	}
}
