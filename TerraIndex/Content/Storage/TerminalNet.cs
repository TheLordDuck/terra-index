using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using TerraIndex.Content.Building;
using TerraIndex.Content.Items;
using TerraIndex.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TerraIndex.Content.Storage
{
	// Multiplayer: clients don't know the contents of chests they haven't opened,
	// so they ask the server to scan, and it replies with what is in the network and where.
	// Withdrawing and depositing also happen on the server, which then tells every client to animate the terminal's door.
	public static class TerminalNet
	{
		private enum MessageType : byte
		{
			RequestContents,
			Contents,
			Withdraw,
			DoorOpen,
			DepositRequest,
			DepositOpen,
			DepositDenied,
			DepositUpdate,
			DepositClose,
			DepositResult,
			QuickDeposit,
			Craft,
			CraftResult,
			TakeItems,
			RadioToggle,
			RadioPlay,
			ChestMoves,
			ItemsEntering,
			Drone,
			Build,
			BuildResult,
			TeleporterLink,
		}

		// A ModPacket can't exceed ~65 KB; large networks are sent in several chunks.
		private const int ChunkBudget = 30000;
		private const int MinTicksBetweenRequests = 10;
		// Main inventory slots, the most a quick deposit can send.
		private const int MaxQuickDepositItems = 40;
		// Every inventory slot, the most a craft can take items from.
		private const int MaxCraftItems = 59;

		private static Mod Mod => ModContent.GetInstance<TerminalSystem>().Mod;

		private static readonly uint[] lastRequest = new uint[Main.maxPlayers];
		private static readonly uint[] lastWithdraw = new uint[Main.maxPlayers];
		private static readonly uint[] lastDeposit = new uint[Main.maxPlayers];
		private static readonly uint[] lastQuickDeposit = new uint[Main.maxPlayers];
		private static readonly uint[] lastCraft = new uint[Main.maxPlayers];
		private static readonly uint[] lastTake = new uint[Main.maxPlayers];
		private static readonly int[] lastReplyHash = new int[Main.maxPlayers];
		// More than any recipe has ingredients.
		private const int MaxTakeItems = 30;
		private static StorageSnapshot pending;

		// force: reply even if nothing changed since the last reply to this player.
		// remote: the terminal is being used through a portable terminal (the panel asks twice a second, so the server
		// always knows; see TerminalSystem.ByRemote).
		public static void SendRequest(Point terminal, bool force, bool remote)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.RequestContents);
			WritePoint(packet, terminal);
			packet.Write(force);
			packet.Write(remote);
			packet.Send();
		}

		public static void SendWithdraw(Point terminal, int type, int amount)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.Withdraw);
			WritePoint(packet, terminal);
			packet.Write(type);
			packet.Write(amount);
			packet.Send();
		}

		// Server -> every client.
		public static void SendDoorOpen(Point terminal)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.DoorOpen);
			WritePoint(packet, terminal);
			packet.Send();
		}

		public static void Handle(BinaryReader reader, int whoAmI)
		{
			var message = (MessageType)reader.ReadByte();
			switch (message) {
				case MessageType.RequestContents: {
					Point terminal = ReadPoint(reader);
					bool force = reader.ReadBoolean();
					bool remote = reader.ReadBoolean();
					if (Main.netMode == NetmodeID.Server)
						TerminalSystem.SetRemote(whoAmI, remote && RemoteTerminalItem.IsLinkedTo(Main.player[whoAmI], terminal));
					HandleRequest(terminal, force, whoAmI);
					break;
				}
				case MessageType.Contents:
					HandleContents(reader);
					break;
				case MessageType.Withdraw:
					HandleWithdraw(ReadPoint(reader), reader.ReadInt32(), reader.ReadInt32(), whoAmI);
					break;
				case MessageType.DoorOpen:
					ModContent.GetInstance<TerminalSystem>().StartDoor(ReadPoint(reader));
					break;
				case MessageType.DepositRequest:
					HandleDepositRequest(ReadPoint(reader), whoAmI);
					break;
				case MessageType.DepositOpen: {
					Point terminal = ReadPoint(reader);
					ModContent.GetInstance<TerminalSystem>().ShowDeposit(terminal, ReadItems(reader, TerminalEntity.Slots));
					break;
				}
				case MessageType.DepositDenied:
					ModContent.GetInstance<TerminalSystem>().ShowDepositDenied();
					break;
				case MessageType.DepositUpdate:
				case MessageType.DepositClose: {
					Point terminal = ReadPoint(reader);
					Item[] items = ReadItems(reader, TerminalEntity.Slots);
					if (Main.netMode == NetmodeID.Server)
						ModContent.GetInstance<TerminalSystem>().ReceiveDeposit(terminal, whoAmI, items, message == MessageType.DepositClose);
					break;
				}
				case MessageType.DepositResult:
					ModContent.GetInstance<TerminalSystem>().ShowDepositResult(reader.ReadInt32(), reader.ReadBoolean(), reader.ReadBoolean());
					break;
				case MessageType.QuickDeposit: {
					Point terminal = ReadPoint(reader);
					Item[] items = ReadItems(reader, MaxQuickDepositItems);
					// The client already took the items out of its inventory: if the request is refused, they are given back.
					if (Main.netMode == NetmodeID.Server)
						ModContent.GetInstance<TerminalSystem>().ReceiveQuickDeposit(terminal, whoAmI, items, Allowed(terminal, whoAmI, lastQuickDeposit));
					break;
				}
				case MessageType.Craft: {
					Point terminal = ReadPoint(reader);
					int recipe = reader.ReadInt32();
					int times = reader.ReadInt32();
					Item[] provided = ReadItems(reader, MaxCraftItems);
					// Like a quick deposit: the client already took these out of its inventory, so a refused request
					// still gets them back.
					if (Main.netMode == NetmodeID.Server)
						ModContent.GetInstance<TerminalSystem>().Craft(terminal, recipe, times, Main.player[whoAmI], provided, Allowed(terminal, whoAmI, lastCraft));
					break;
				}
				case MessageType.CraftResult:
					ModContent.GetInstance<TerminalSystem>().ShowCraftResult(reader.ReadInt32(), reader.ReadInt32());
					break;
				case MessageType.TakeItems: {
					Point terminal = ReadPoint(reader);
					var items = new List<(int Type, int Amount)>();
					for (int count = Math.Min((int)reader.ReadByte(), MaxTakeItems); count > 0; count--)
						items.Add((reader.ReadInt32(), reader.ReadInt32()));
					if (Allowed(terminal, whoAmI, lastTake))
						ModContent.GetInstance<TerminalSystem>().TakeItems(terminal, items, Main.player[whoAmI]);
					break;
				}
				case MessageType.RadioToggle: {
					Point radio = ReadPoint(reader);
					// Within reach of the player, like any other tile interaction.
					if (Main.netMode == NetmodeID.Server && WorldGen.InWorld(radio.X, radio.Y)
						&& Main.player[whoAmI].Distance(RadioTile.Center(radio)) < 30 * 16)
						RadioTile.Toggle(radio);
					break;
				}
				case MessageType.RadioPlay: {
					int clip = reader.ReadByte();
					var radios = new List<Point>();
					for (int count = reader.ReadByte(); count > 0; count--)
						radios.Add(ReadPoint(reader));
					ModContent.GetInstance<RadioSystem>().Play(clip, radios);
					break;
				}
				case MessageType.ChestMoves: {
					var moves = new List<ChestMove>();
					for (int count = Math.Min((int)reader.ReadByte(), StorageNetwork.MaxMoves); count > 0; count--)
						moves.Add(new ChestMove(ReadPoint(reader), reader.ReadInt32()));
					ModContent.GetInstance<NetworkEffects>().Show(moves);
					break;
				}
				case MessageType.Drone: {
					int player = reader.ReadByte();
					ModContent.GetInstance<NetworkEffects>().ShowDrone(player, reader.ReadInt32());
					break;
				}
				case MessageType.ItemsEntering: {
					Point terminal = ReadPoint(reader);
					Player player = Main.player[reader.ReadByte()];
					bool remote = reader.ReadBoolean();
					var types = new List<int>();
					for (int count = reader.ReadByte(); count > 0; count--)
						types.Add(reader.ReadInt32());
					ModContent.GetInstance<TerminalSystem>().ShowItemsEntering(terminal, player, types, remote);
					break;
				}
				case MessageType.Build: {
					int blueprint = reader.ReadByte();
					int block = reader.ReadInt32();
					Point origin = ReadPoint(reader);
					if (Main.netMode == NetmodeID.Server)
						ModContent.GetInstance<BuildSystem>().ReceiveBuild(whoAmI, blueprint, block, origin);
					break;
				}
				case MessageType.BuildResult: {
					bool ok = reader.ReadBoolean();
					int blueprint = reader.ReadByte();
					ModContent.GetInstance<BuildSystem>().ShowBuildResult(ok, blueprint, ReadPoint(reader));
					break;
				}
				case MessageType.TeleporterLink: {
					Point a = ReadPoint(reader);
					Point b = ReadPoint(reader);
					// The second one was just clicked, so the player must be near it.
					if (Main.netMode == NetmodeID.Server && Main.player[whoAmI].Distance(WirelessTeleporterTile.Center(b)) < 30 * 16)
						ModContent.GetInstance<TeleporterSystem>().Link(a, b);
					break;
				}
			}
		}

		// Server -> every client: a drone bringing an item of this type to this player (portable terminal).
		public static void SendDrone(int player, int type)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.Drone);
			packet.Write((byte)player);
			packet.Write(type);
			packet.Send();
		}

		// Server -> every client except the depositing player (who already shows it): items flying into the door (or a drone).
		public static void SendItemsEntering(Point terminal, int player, List<int> types, bool remote)
		{
			if (types.Count == 0)
				return;
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.ItemsEntering);
			WritePoint(packet, terminal);
			packet.Write((byte)player);
			packet.Write(remote);
			packet.Write((byte)types.Count);
			foreach (int type in types)
				packet.Write(type);
			packet.Send(-1, player);
		}

		// Server -> every client: what was taken out of which chests, for the chest effects.
		public static void SendChestMoves(List<ChestMove> moves)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.ChestMoves);
			packet.Write((byte)moves.Count);
			foreach (ChestMove move in moves) {
				WritePoint(packet, move.Chest);
				packet.Write(move.Type);
			}
			packet.Send();
		}

		// Client -> server: build this blueprint here. used: the materials the client already took out of its inventory,
		// given back if the server can't build it (blueprints aren't part of the terminal, but share the mod's packets).
		public static void SendBuild(int blueprint, int block, Point origin)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.Build);
			packet.Write((byte)blueprint);
			packet.Write(block);
			WritePoint(packet, origin);
			packet.Send();
		}

		// Server -> the builder.
		public static void SendBuildResult(int toClient, bool ok, int blueprint, Point origin)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.BuildResult);
			packet.Write(ok);
			packet.Write((byte)blueprint);
			WritePoint(packet, origin);
			packet.Send(toClient);
		}

		// Client -> server: links two wireless teleporters (see TeleporterSystem).
		public static void SendTeleporterLink(Point a, Point b)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.TeleporterLink);
			WritePoint(packet, a);
			WritePoint(packet, b);
			packet.Send();
		}

		// Client -> server: turns a radio on or off (not part of the terminal, but it shares the mod's packets).
		public static void SendRadioToggle(Point radio)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.RadioToggle);
			WritePoint(packet, radio);
			packet.Send();
		}

		// Server -> every client: play this clip now, at the nearest of these radios (see RadioSystem).
		public static void SendRadioPlay(int clip, List<Point> radios)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.RadioPlay);
			packet.Write((byte)clip);
			packet.Write((byte)radios.Count);
			foreach (Point radio in radios)
				WritePoint(packet, radio);
			packet.Send();
		}

		// Client -> server: asks to open the terminal's deposit slots.
		public static void SendDepositRequest(Point terminal)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.DepositRequest);
			WritePoint(packet, terminal);
			packet.Send();
		}

		// Client -> server: the slots' current contents; on close the server also sends them to the chests.
		public static void SendDepositItems(Point terminal, Item[] items, bool close)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)(close ? MessageType.DepositClose : MessageType.DepositUpdate));
			WritePoint(packet, terminal);
			WriteItems(packet, items);
			packet.Send();
		}

		// Client -> server: items already taken out of the inventory by the "Deposit all" button.
		public static void SendQuickDeposit(Point terminal, Item[] items)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.QuickDeposit);
			WritePoint(packet, terminal);
			WriteItems(packet, items);
			packet.Send();
		}

		// Client -> server: craft the recipe (index in Main.recipe) this many times from the network's chests, plus the
		// items already taken out of the inventory for what the chests lack.
		public static void SendCraft(Point terminal, int recipe, int times, Item[] provided)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.Craft);
			WritePoint(packet, terminal);
			packet.Write(recipe);
			packet.Write(times);
			WriteItems(packet, provided);
			packet.Send();
		}

		// Client -> server: take these amounts of these items out of the chests ("Take missing" in the recipe index).
		public static void SendTakeItems(Point terminal, List<(int Type, int Amount)> items)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.TakeItems);
			WritePoint(packet, terminal);
			packet.Write((byte)Math.Min(items.Count, MaxTakeItems));
			foreach (var (type, amount) in items.Take(MaxTakeItems)) {
				packet.Write(type);
				packet.Write(amount);
			}
			packet.Send();
		}

		// Server -> client: how many items were crafted (0 = the chests didn't have the ingredients).
		public static void SendCraftResult(int toClient, int type, int amount)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.CraftResult);
			packet.Write(type);
			packet.Write(amount);
			packet.Send(toClient);
		}

		// Server -> client. returned: leftovers went back to the player (quick deposit) instead of staying in the terminal.
		public static void SendDepositResult(int toClient, int left, bool moved, bool returned)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.DepositResult);
			packet.Write(left);
			packet.Write(moved);
			packet.Write(returned);
			packet.Send(toClient);
		}

		private static void HandleDepositRequest(Point terminal, int whoAmI)
		{
			if (!Allowed(terminal, whoAmI, lastDeposit))
				return;

			Item[] items = ModContent.GetInstance<TerminalSystem>().BeginDeposit(terminal, whoAmI);
			ModPacket packet = Mod.GetPacket();
			if (items == null) {
				packet.Write((byte)MessageType.DepositDenied);
			}
			else {
				packet.Write((byte)MessageType.DepositOpen);
				WritePoint(packet, terminal);
				WriteItems(packet, items);
			}
			packet.Send(whoAmI);
		}

		private static void WriteItems(BinaryWriter writer, Item[] items)
		{
			writer.Write((byte)items.Length);
			foreach (Item item in items)
				ItemIO.Send(item, writer, writeStack: true);
		}

		private static Item[] ReadItems(BinaryReader reader, int max)
		{
			var items = new Item[Math.Min((int)reader.ReadByte(), max)];
			for (int i = 0; i < items.Length; i++)
				items[i] = ItemIO.Receive(reader, readStack: true);
			return items;
		}

		// Basic checks so a client can't use distant networks or flood the server.
		private static bool Allowed(Point terminal, int whoAmI, uint[] lastTick)
		{
			if (Main.netMode != NetmodeID.Server || !WorldGen.InWorld(terminal.X, terminal.Y))
				return false;
			if (!TerminalTile.IsTerminal(terminal) || !RemoteTerminalItem.CanReach(Main.player[whoAmI], terminal))
				return false;
			if (Main.GameUpdateCount - lastTick[whoAmI] < MinTicksBetweenRequests)
				return false;
			lastTick[whoAmI] = Main.GameUpdateCount;
			return true;
		}

		// Panels refresh twice a second and the contents rarely change, so the server remembers a hash of its last
		// reply to each player and skips identical ones (unless forced). Big networks are tens of KB per reply.
		private static void HandleRequest(Point terminal, bool force, int whoAmI)
		{
			if (!Allowed(terminal, whoAmI, lastRequest))
				return;
			StorageSnapshot snapshot = StorageNetwork.Scan(terminal);
			int hash = HashCode.Combine(terminal, snapshot.Hash());
			if (!force && lastReplyHash[whoAmI] == hash)
				return;
			lastReplyHash[whoAmI] = hash;
			SendContents(terminal, snapshot, whoAmI);
		}

		private static void HandleWithdraw(Point terminal, int type, int amount, int whoAmI)
		{
			if (Allowed(terminal, whoAmI, lastWithdraw))
				ModContent.GetInstance<TerminalSystem>().Dispense(terminal, type, Math.Max(0, amount), Main.player[whoAmI]);
		}

		private static void SendContents(Point terminal, StorageSnapshot snapshot, int toClient)
		{
			var entries = snapshot.Totals.ToList();
			int next = 0;
			bool first = true;

			do {
				int start = next;
				int bytes = 0;
				while (next < entries.Count) {
					int size = 10 + 4 * snapshot.Locations[entries[next].Key].Count;
					if (next > start && bytes + size > ChunkBudget)
						break;
					bytes += size;
					next++;
				}

				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)MessageType.Contents);
				WritePoint(packet, terminal);
				packet.Write(first);
				packet.Write(next == entries.Count);
				packet.Write(snapshot.ChestCount);
				packet.Write(snapshot.Slots);
				packet.Write(snapshot.UsedSlots);
				// The crafting stations on the wire go only in the first chunk.
				if (first) {
					packet.Write((short)snapshot.Stations.Count);
					foreach (int station in snapshot.Stations)
						packet.Write(station);
				}
				packet.Write(next - start);
				for (int i = start; i < next; i++) {
					var (type, stack) = entries[i];
					List<Point> locations = snapshot.Locations[type];
					packet.Write(type);
					packet.Write(stack);
					packet.Write((short)locations.Count);
					foreach (Point location in locations)
						WritePoint(packet, location);
				}
				packet.Send(toClient);

				first = false;
			} while (next < entries.Count);
		}

		private static void HandleContents(BinaryReader reader)
		{
			Point terminal = ReadPoint(reader);
			bool first = reader.ReadBoolean();
			bool last = reader.ReadBoolean();
			int chestCount = reader.ReadInt32();
			int slots = reader.ReadInt32();
			int usedSlots = reader.ReadInt32();
			var stations = new List<int>();
			if (first) {
				for (int i = reader.ReadInt16(); i > 0; i--)
					stations.Add(reader.ReadInt32());
			}
			int count = reader.ReadInt32();

			if (first || pending == null) {
				pending = new StorageSnapshot { ChestCount = chestCount, Slots = slots, UsedSlots = usedSlots };
				pending.Stations.UnionWith(stations);
			}

			for (int i = 0; i < count; i++) {
				int type = reader.ReadInt32();
				int stack = reader.ReadInt32();
				int locationCount = reader.ReadInt16();
				var locations = new List<Point>(locationCount);
				for (int j = 0; j < locationCount; j++)
					locations.Add(ReadPoint(reader));
				pending.Totals[type] = stack;
				pending.Locations[type] = locations;
			}

			if (last) {
				ModContent.GetInstance<TerminalSystem>().ApplyRemote(terminal, pending);
				pending = null;
			}
		}

		private static void WritePoint(BinaryWriter writer, Point point)
		{
			writer.Write((short)point.X);
			writer.Write((short)point.Y);
		}

		private static Point ReadPoint(BinaryReader reader) => new(reader.ReadInt16(), reader.ReadInt16());
	}
}
