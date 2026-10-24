using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TerraIndex.Common;
using TerraIndex.Content.Tiles;
using Terraria;
using Terraria.ID;

namespace TerraIndex.Content.Storage
{
	// Result of scanning a network: what is stored and where. This is what the server sends to clients.
	public class StorageSnapshot
	{
		public int ChestCount;
		// Item slots in the connected chests, and how many of them hold something.
		public int Slots;
		public int UsedSlots;
		public readonly Dictionary<int, int> Totals = new();
		public readonly Dictionary<int, List<Point>> Locations = new();
		// Crafting station tiles touched by the network's wire (see RecipeDatabase.IsStation).
		public readonly HashSet<int> Stations = new();

		// Same contents give the same hash, whatever order the chests were walked in.
		public int Hash()
		{
			int hash = HashCode.Combine(ChestCount, Slots, UsedSlots);
			unchecked {
				foreach (var (type, stack) in Totals)
					hash += HashCode.Combine(type, stack);
				foreach (var (type, chests) in Locations) {
					foreach (Point chest in chests)
						hash += HashCode.Combine(type, chest) * 31;
				}
				foreach (int station in Stations)
					hash += HashCode.Combine(-1, station) * 17;
			}
			return hash;
		}
	}

	// An item type taken out of a chest, shown by the chest effects (see NetworkEffects).
	public readonly record struct ChestMove(Point Chest, int Type);

	public static class StorageNetwork
	{
		// Moves made by Withdraw, Deposit and TakeIngredients since TerminalSystem last showed them. One per chest,
		// item type and direction, capped so a huge deposit doesn't send hundreds.
		public const int MaxMoves = 16;
		public static readonly List<ChestMove> Moves = new();

		private static void Record(int index, int type)
		{
			Chest chest = Main.chest[index];
			var move = new ChestMove(new Point(chest.x, chest.y), type);
			if (Moves.Count < MaxMoves && !Moves.Contains(move))
				Moves.Add(move);
		}

		// Cap on wire tiles to walk, so a huge network can't freeze the game.
		private static int MaxWireTiles => ServerConfig.Instance.MaxWireTiles;

		private static readonly Point[] Neighbors = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

		// Scan's wire walk per terminal, reused for a second: open panels scan twice a second for every player, and the
		// wire rarely changes. Withdrawing, depositing and crafting always walk the wire again.
		private const int WalkCacheTicks = 60;
		private static readonly Dictionary<Point, (uint Tick, List<int> Chests, HashSet<int> Stations)> walkCache = new();

		public static void ClearCache() => walkCache.Clear();

		// Only call this where every chest is known: in single player or on the server.
		public static StorageSnapshot Scan(Point terminal)
		{
			var snapshot = new StorageSnapshot();
			if (!walkCache.TryGetValue(terminal, out var walk) || Main.GameUpdateCount - walk.Tick >= WalkCacheTicks) {
				var stations = new HashSet<int>();
				walk = (Main.GameUpdateCount, Walk(terminal, stations), stations);
				walkCache[terminal] = walk;
			}
			snapshot.Stations.UnionWith(walk.Stations);
			List<int> chests = walk.Chests;
			snapshot.ChestCount = chests.Count;

			foreach (int index in chests) {
				Chest chest = Main.chest[index];
				if (chest?.item == null)
					continue;

				var position = new Point(chest.x, chest.y);
				snapshot.Slots += chest.item.Length;
				foreach (Item item in chest.item) {
					if (item == null || item.IsAir)
						continue;

					snapshot.UsedSlots++;
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
					Record(index, type);
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
				int type = item.type;

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

		// Removes the ingredients of `times` crafts of the recipe from the chests: all of them, or nothing if any is short.
		// Group ingredients ("Any Wood") take the listed item first, then any other member.
		// Items in extra (type -> amount, already out of a player's inventory) are used before the chests; on success,
		// extra is left with what wasn't used, and on failure it isn't touched.
		// Like Scan, only call this in single player or on the server; changed slots are synced to clients.
		public static bool TakeIngredients(List<int> chests, Recipe recipe, int times, Dictionary<int, int> extra)
		{
			var left = new Dictionary<int, int>(extra);
			var available = new Dictionary<int, int>();
			foreach (int index in chests) {
				foreach (Item item in Main.chest[index]?.item ?? Array.Empty<Item>()) {
					if (item != null && !item.IsAir)
						available[item.type] = available.GetValueOrDefault(item.type) + item.stack;
				}
			}

			var taking = new List<(int Type, int Amount)>();
			foreach (Item ingredient in recipe.requiredItem) {
				int needed = ingredient.stack * times;
				List<int> accepted = RecipeDatabase.AcceptedItems(recipe, ingredient.type).ToList();
				foreach (int type in accepted) {
					int use = Math.Min(left.GetValueOrDefault(type), needed);
					if (use <= 0)
						continue;
					left[type] -= use;
					needed -= use;
				}
				foreach (int type in accepted) {
					if (needed == 0)
						break;
					int take = Math.Min(available.GetValueOrDefault(type), needed);
					if (take <= 0)
						continue;
					available[type] -= take;
					needed -= take;
					taking.Add((type, take));
				}
				if (needed > 0)
					return false;
			}

			foreach (var (type, amount) in taking)
				Remove(chests, type, amount);
			foreach (var (type, amount) in left)
				extra[type] = amount;
			return true;
		}

		private static void Remove(List<int> chests, int type, int amount)
		{
			foreach (int index in chests) {
				Item[] slots = Main.chest[index]?.item;
				if (slots == null)
					continue;
				for (int slot = 0; slot < slots.Length && amount > 0; slot++) {
					Item item = slots[slot];
					if (item == null || item.IsAir || item.type != type)
						continue;
					int taken = Math.Min(item.stack, amount);
					item.stack -= taken;
					amount -= taken;
					if (item.stack <= 0)
						item.TurnToAir();
					Record(index, type);
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncChestItem, number: index, number2: slot);
				}
				if (amount <= 0)
					return;
			}
		}

		// Walks the red wire starting from the terminal's tiles and returns the chests it passes through.
		// If the wire doesn't touch the terminal there is no network: the terminal must be on the same wire as the chests.
		public static List<int> FindConnectedChests(Point terminal) => Walk(terminal, null);

		// Same walk; when stations isn't null, it also collects the crafting stations the wire passes over.
		private static List<int> Walk(Point terminal, HashSet<int> stations)
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
				if (stations != null) {
					Tile tile = Main.tile[p.X, p.Y];
					if (tile.HasTile && RecipeDatabase.IsStation(tile.TileType))
						stations.Add(tile.TileType);
				}

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
