using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TerraIndex.Content.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TerraIndex.Content.Storage
{
	// Holds the items left in a terminal's deposit slots that didn't fit in the chests. Saved with the world.
	// Only used in single player and on the server; it is created the first time someone deposits.
	public class TerminalEntity : ModTileEntity
	{
		public const int Slots = 20;

		// Pending items are retried every 2 seconds, in case room was made in the chests.
		private const int RetryInterval = 120;
		private const float MaxUserDistance = 16 * 20;

		public readonly Item[] Items = Enumerable.Range(0, Slots).Select(_ => new Item()).ToArray();

		// Player with the deposit slots open, or -1. Only one at a time, so two players can't duplicate items.
		public int User = -1;

		private int retryTimer;

		public Point TopLeft => new(Position.X, Position.Y);

		public bool HasItems => Items.Any(item => !item.IsAir);

		public override bool IsTileValidForEntity(int x, int y)
		{
			Tile tile = Main.tile[x, y];
			return tile.HasTile && tile.TileType == ModContent.TileType<TerminalTile>()
				&& tile.TileFrameX % (TerminalTile.Width * 18) == 0 && tile.TileFrameY % (TerminalTile.Height * 18) == 0;
		}

		public static TerminalEntity Get(Point topLeft, bool create)
		{
			if (TileEntity.ByPosition.TryGetValue(new Point16(topLeft.X, topLeft.Y), out TileEntity entity) && entity is TerminalEntity terminal)
				return terminal;
			if (!create || Main.netMode == NetmodeID.MultiplayerClient || !TerminalTile.IsTerminal(topLeft))
				return null;
			int id = ModContent.GetInstance<TerminalEntity>().Place(topLeft.X, topLeft.Y);
			return TileEntity.ByID[id] as TerminalEntity;
		}

		public override void Update()
		{
			if (User >= 0) {
				Player player = Main.player[User];
				if (!player.active || player.dead || player.Distance(TerminalTile.Center(TopLeft)) > MaxUserDistance)
					User = -1;
				return;
			}

			if (HasItems && ++retryTimer >= RetryInterval) {
				retryTimer = 0;
				ModContent.GetInstance<TerminalSystem>().Flush(this, -1);
			}
		}

		public override void SaveData(TagCompound tag)
		{
			List<TagCompound> items = Items.Where(item => !item.IsAir).Select(ItemIO.Save).ToList();
			if (items.Count > 0)
				tag["items"] = items;
		}

		public override void LoadData(TagCompound tag)
		{
			IList<TagCompound> items = tag.GetList<TagCompound>("items");
			for (int i = 0; i < items.Count && i < Slots; i++)
				Items[i] = ItemIO.Load(items[i]);
		}
	}
}
