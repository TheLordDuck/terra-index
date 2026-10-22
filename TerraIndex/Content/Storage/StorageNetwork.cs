using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TerraIndex.Content.Tiles;
using Terraria;
using Terraria.ID;

namespace TerraIndex.Content.Storage
{
	// Result of scanning a network: what is stored and where. This is what the server sends to clients.
	public class StorageSnapshot
	{
		public int ChestCount;
		public readonly Dictionary<int, int> Totals = new();
		public readonly Dictionary<int, List<Point>> Locations = new();
	}

	public static class StorageNetwork
	{
		// Cap on wire tiles to walk, so a huge network can't freeze the game.
		private const int MaxWireTiles = 50000;

		private static readonly Point[] Neighbors = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

		// Only call this where every chest is known: in single player or on the server.
		public static StorageSnapshot Scan(Point terminal)
		{
			var snapshot = new StorageSnapshot();
			List<int> chests = FindConnectedChests(terminal);
			snapshot.ChestCount = chests.Count;

			foreach (int index in chests) {
				Chest chest = Main.chest[index];
				if (chest?.item == null)
					continue;

				var position = new Point(chest.x, chest.y);
				foreach (Item item in chest.item) {
					if (item == null || item.IsAir)
						continue;

					snapshot.Totals[item.type] = snapshot.Totals.GetValueOrDefault(item.type) + item.stack;
					if (!snapshot.Locations.TryGetValue(item.type, out List<Point> list))
						snapshot.Locations[item.type] = list = new List<Point>();
					// Chests are walked one at a time, so checking the last entry is enough to avoid duplicates.
					if (list.Count == 0 || list[^1] != position)
						list.Add(position);
				}
			}

			return snapshot;
		}

		// Takes up to one full stack of the item (or only amount, when above 0) out of the connected chests. Returns null if there is none.
		// Only stacks identical to the first one found (same prefix) are merged.
		// Like Scan, only call this in single player or on the server; changed slots are synced to clients.
		public static Item Withdraw(Point terminal, int type, int amount = 0)
		{
			Item taken = null;
			foreach (int index in FindConnectedChests(terminal)) {
				Chest chest = Main.chest[index];
				if (chest?.item == null)
					continue;

				for (int slot = 0; slot < chest.item.Length; slot++) {
					Item item = chest.item[slot];
					if (item == null || item.IsAir || item.type != type)
						continue;
					if (taken == null) {
						taken = item.Clone();
						taken.stack = 0;
					}
					else if (item.prefix != taken.prefix) {
						continue;
					}

					int limit = amount > 0 ? Math.Min(amount, taken.maxStack) : taken.maxStack;
					int moved = Math.Min(item.stack, limit - taken.stack);
					taken.stack += moved;
					item.stack -= moved;
					if (item.stack <= 0)
						item.TurnToAir();
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncChestItem, number: index, number2: slot);

					if (taken.stack >= limit)
						return taken;
				}
			}
			return taken;
		}

		// Moves the items into the connected chests: first onto matching stacks, then into empty slots.
		// Whatever doesn't fit stays in the array. Returns true if anything was moved.
		// Like Scan, only call this in single player or on the server; changed slots are synced to clients.
		public static bool Deposit(Point terminal, Item[] items)
		{
			List<int> chests = FindConnectedChests(terminal);
			bool moved = false;

			foreach (Item item in items) {
				if (item == null || item.IsAir)
					continue;

				for (int pass = 0; pass < 2 && !item.IsAir; pass++) {
					foreach (int index in chests) {
						Item[] slots = Main.chest[index]?.item;
						if (slots == null)
							continue;

						for (int slot = 0; slot < slots.Length && !item.IsAir; slot++) {
							Item target = slots[slot];
							if (pass == 0) {
								if (target == null || target.IsAir || target.type != item.type || target.prefix != item.prefix || target.stack >= target.maxStack)
									continue;
								int amount = Math.Min(item.stack, target.maxStack - target.stack);
								target.stack += amount;
								item.stack -= amount;
								if (item.stack <= 0)
									item.TurnToAir();
							}
							else {
								if (target != null && !target.IsAir)
									continue;
								slots[slot] = item.Clone();
								item.TurnToAir();
							}

							moved = true;
							if (Main.netMode == NetmodeID.Server)
								NetMessage.SendData(MessageID.SyncChestItem, number: index, number2: slot);
						}
						if (item.IsAir)
							break;
					}
				}
			}

			return moved;
		}

		// Walks the red wire starting from the terminal's tiles and returns the chests it passes through.
		// If the wire doesn't touch the terminal there is no network: the terminal must be on the same wire as the chests.
		public static List<int> FindConnectedChests(Point terminal)
		{
			var chests = new HashSet<int>();
			var visited = new HashSet<Point>();
			var queue = new Queue<Point>();

			for (int x = 0; x < TerminalTile.Width; x++) {
				for (int y = 0; y < TerminalTile.Height; y++) {
					var start = new Point(terminal.X + x, terminal.Y + y);
					if (HasRedWire(start) && visited.Add(start))
						queue.Enqueue(start);
				}
			}

			while (queue.Count > 0 && visited.Count < MaxWireTiles) {
				Point p = queue.Dequeue();

				int chest = ChestAt(p);
				if (chest >= 0)
					chests.Add(chest);

				foreach (Point offset in Neighbors) {
					var next = new Point(p.X + offset.X, p.Y + offset.Y);
					if (HasRedWire(next) && visited.Add(next))
						queue.Enqueue(next);
				}
			}

			return new List<int>(chests);
		}

		// The chest that covers this tile, whichever of its tiles it is. (Chest.FindChestByGuessing only matches
		// a chest's left column, so wire running over the right half of a chest was missed.)
		private static int ChestAt(Point p)
		{
			Tile tile = Main.tile[p.X, p.Y];
			if (!tile.HasTile || !Main.tileContainer[tile.TileType])
				return -1;
			int width = TileID.Sets.BasicDresser[tile.TileType] ? 3 : 2;
			return Chest.FindChest(p.X - tile.TileFrameX / 18 % width, p.Y - tile.TileFrameY / 18 % 2);
		}

		private static bool HasRedWire(Point p) => WorldGen.InWorld(p.X, p.Y, 1) && Main.tile[p.X, p.Y].RedWire;
	}
}
