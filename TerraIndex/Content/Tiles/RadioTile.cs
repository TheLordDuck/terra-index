using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TerraIndex.Content.Items;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TerraIndex.Content.Tiles
{
	// 2x1 radio that stands on the floor, like a Piggy Bank. Right click turns it on or off.
	// Frames 0-1 (TileFrameX 0-35) are the radio on, frames 2-3 (36-71) off: lit dial and green light, or dark.
	public class RadioTile : ModTile
	{
		public const int Width = 2;
		private const int StateWidth = Width * 18;

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2x1);
			TileObjectData.newTile.CoordinateHeights = new[] { 18 };
			TileObjectData.addTile(Type);

			AddMapEntry(new Color(150, 45, 35), CreateMapEntryName());
			DustType = DustID.Iron;
		}

		// The off frames would count as another style, so the drop is set here instead of by style.
		public override IEnumerable<Item> GetItemDrops(int i, int j)
		{
			yield return new Item(ModContent.ItemType<RadioItem>());
		}

		public static Point TopLeft(int i, int j) => new(i - Main.tile[i, j].TileFrameX % StateWidth / 18, j);

		public static bool IsRadio(Point topLeft)
		{
			Tile tile = Main.tile[topLeft.X, topLeft.Y];
			return tile.HasTile && tile.TileType == ModContent.TileType<RadioTile>();
		}

		public static bool IsOn(Point topLeft) => IsRadio(topLeft) && Main.tile[topLeft.X, topLeft.Y].TileFrameX < StateWidth;

		public static Vector2 Center(Point topLeft) => new(topLeft.X * 16 + Width * 8, topLeft.Y * 16 + 8);

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<RadioItem>();
		}

		public override bool RightClick(int i, int j)
		{
			Point topLeft = TopLeft(i, j);
			SoundEngine.PlaySound(SoundID.Mech, Center(topLeft));
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TerminalNet.SendRadioToggle(topLeft);
			else
				Toggle(topLeft);
			return true;
		}

		// Single player or server: switches both halves between the on and off frames and syncs them.
		public static void Toggle(Point topLeft)
		{
			if (!IsRadio(topLeft))
				return;
			for (int x = 0; x < Width; x++) {
				Tile tile = Main.tile[topLeft.X + x, topLeft.Y];
				tile.TileFrameX = (short)((tile.TileFrameX + StateWidth) % (StateWidth * 2));
			}
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendTileSquare(-1, topLeft.X, topLeft.Y, Width, 1);
		}
	}
}
