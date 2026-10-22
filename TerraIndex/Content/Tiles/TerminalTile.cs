using Microsoft.Xna.Framework;
using TerraIndex.Content.Items;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TerraIndex.Content.Tiles
{
	// 4x2 terminal that hangs on a background wall, like a painting.
	// Left half: the screen. Right half: a garage door that rolls up to hand out withdrawn items (see TerminalSystem).
	public class TerminalTile : ModTile
	{
		public const int Width = 4;
		public const int Height = 2;

		// Door opening in pixels, relative to the terminal's top-left corner. Must match TerminalTile.png and TerminalDoor.png.
		public static readonly Rectangle DoorArea = new(37, 5, 22, 21);

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.FramesOnKillWall[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
			TileObjectData.newTile.Width = Width;
			TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
			TileObjectData.newTile.AnchorWall = true;
			TileObjectData.newTile.Origin = new Point16(0, 0);
			TileObjectData.addTile(Type);

			AddMapEntry(new Color(80, 200, 255), CreateMapEntryName());
			DustType = DustID.Stone;
		}

		public static Point TopLeft(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			return new Point(i - tile.TileFrameX % (Width * 18) / 18, j - tile.TileFrameY % (Height * 18) / 18);
		}

		public static bool IsTerminal(Point topLeft)
		{
			Tile tile = Main.tile[topLeft.X, topLeft.Y];
			return tile.HasTile && tile.TileType == ModContent.TileType<TerminalTile>();
		}

		public static Vector2 Center(Point topLeft) => new(topLeft.X * 16 + Width * 8, topLeft.Y * 16 + Height * 8);

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<TerminalItem>();
		}

		// Items still waiting in the terminal are dropped instead of being lost.
		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			TerminalEntity entity = TerminalEntity.Get(new Point(i, j), false);
			if (entity == null)
				return;
			var area = new Rectangle(i * 16, j * 16, Width * 16, Height * 16);
			foreach (Item item in entity.Items) {
				if (!item.IsAir)
					Item.NewItem(new EntitySource_TileBreak(i, j), area, item.Clone());
			}
			entity.Kill(i, j);
		}

		public override bool RightClick(int i, int j)
		{
			ModContent.GetInstance<TerminalSystem>().Open(TopLeft(i, j));
			return true;
		}
	}
}
