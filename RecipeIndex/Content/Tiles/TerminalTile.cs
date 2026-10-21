using Microsoft.Xna.Framework;
using RecipeIndex.Content.Items;
using RecipeIndex.Content.Storage;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace RecipeIndex.Content.Tiles
{
	// Terminal de 2x2 que se cuelga en la pared de fondo, como un cuadro.
	public class TerminalTile : ModTile
	{
		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.FramesOnKillWall[Type] = true;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
			TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
			TileObjectData.newTile.AnchorWall = true;
			TileObjectData.newTile.Origin = new Point16(0, 0);
			TileObjectData.addTile(Type);

			AddMapEntry(new Color(80, 200, 255), CreateMapEntryName());
			DustType = DustID.Stone;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.noThrow = 2;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<TerminalItem>();
		}

		public override bool RightClick(int i, int j)
		{
			Tile tile = Main.tile[i, j];
			var topLeft = new Point(i - tile.TileFrameX % 36 / 18, j - tile.TileFrameY % 36 / 18);
			ModContent.GetInstance<TerminalSystem>().Open(topLeft);
			return true;
		}
	}
}
