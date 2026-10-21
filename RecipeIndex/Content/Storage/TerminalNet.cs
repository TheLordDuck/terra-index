using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using RecipeIndex.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace RecipeIndex.Content.Storage
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
			ItemsEntering,
		}

		// A ModPacket can't exceed ~65 KB; large networks are sent in several chunks.
		private const int ChunkBudget = 30000;
		private const float MaxRequestDistance = 16 * 20;
		private const int MinTicksBetweenRequests = 10;

		private static Mod Mod => ModContent.GetInstance<TerminalSystem>().Mod;

		private static readonly uint[] lastRequest = new uint[Main.maxPlayers];
		private static readonly uint[] lastWithdraw = new uint[Main.maxPlayers];
		private static readonly uint[] lastDeposit = new uint[Main.maxPlayers];
		private static StorageSnapshot pending;

		public static void SendRequest(Point terminal)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.RequestContents);
			WritePoint(packet, terminal);
			packet.Send();
		}

		public static void SendWithdraw(Point terminal, int type)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.Withdraw);
			WritePoint(packet, terminal);
			packet.Write(type);
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
				case MessageType.RequestContents:
					HandleRequest(ReadPoint(reader), whoAmI);
					break;
				case MessageType.Contents:
					HandleContents(reader);
					break;
				case MessageType.Withdraw:
					HandleWithdraw(ReadPoint(reader), reader.ReadInt32(), whoAmI);
					break;
				case MessageType.DoorOpen:
					ModContent.GetInstance<TerminalSystem>().StartDoor(ReadPoint(reader));
					break;
				case MessageType.DepositRequest:
					HandleDepositRequest(ReadPoint(reader), whoAmI);
					break;
				case MessageType.DepositOpen: {
					Point terminal = ReadPoint(reader);
					ModContent.GetInstance<TerminalSystem>().ShowDeposit(terminal, ReadItems(reader));
					break;
				}
				case MessageType.DepositDenied:
					ModContent.GetInstance<TerminalSystem>().ShowDepositDenied();
					break;
				case MessageType.DepositUpdate:
				case MessageType.DepositClose: {
					Point terminal = ReadPoint(reader);
					Item[] items = ReadItems(reader);
					if (Main.netMode == NetmodeID.Server)
						ModContent.GetInstance<TerminalSystem>().ReceiveDeposit(terminal, whoAmI, items, message == MessageType.DepositClose);
					break;
				}
				case MessageType.DepositResult:
					ModContent.GetInstance<TerminalSystem>().ShowDepositResult(reader.ReadInt32(), reader.ReadBoolean());
					break;
				case MessageType.ItemsEntering: {
					Point terminal = ReadPoint(reader);
					Player player = Main.player[reader.ReadByte()];
					var types = new List<int>();
					for (int count = reader.ReadByte(); count > 0; count--)
						types.Add(reader.ReadInt32());
					ModContent.GetInstance<TerminalSystem>().ShowItemsEntering(terminal, player.Center, types);
					break;
				}
			}
		}

		// Server -> every client except the depositing player (who already shows it): items flying into the door.
		public static void SendItemsEntering(Point terminal, int player, List<int> types)
		{
			if (types.Count == 0)
				return;
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.ItemsEntering);
			WritePoint(packet, terminal);
			packet.Write((byte)player);
			packet.Write((byte)types.Count);
			foreach (int type in types)
				packet.Write(type);
			packet.Send(-1, player);
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

		// Server -> client.
		public static void SendDepositResult(int toClient, int left, bool moved)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.DepositResult);
			packet.Write(left);
			packet.Write(moved);
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

		private static Item[] ReadItems(BinaryReader reader)
		{
			var items = new Item[Math.Min((int)reader.ReadByte(), TerminalEntity.Slots)];
			for (int i = 0; i < items.Length; i++)
				items[i] = ItemIO.Receive(reader, readStack: true);
			return items;
		}

		// Basic checks so a client can't use distant networks or flood the server.
		private static bool Allowed(Point terminal, int whoAmI, uint[] lastTick)
		{
			if (Main.netMode != NetmodeID.Server || !WorldGen.InWorld(terminal.X, terminal.Y))
				return false;
			if (!TerminalTile.IsTerminal(terminal) || Main.player[whoAmI].Distance(TerminalTile.Center(terminal)) > MaxRequestDistance)
				return false;
			if (Main.GameUpdateCount - lastTick[whoAmI] < MinTicksBetweenRequests)
				return false;
			lastTick[whoAmI] = Main.GameUpdateCount;
			return true;
		}

		private static void HandleRequest(Point terminal, int whoAmI)
		{
			if (Allowed(terminal, whoAmI, lastRequest))
				SendContents(terminal, StorageNetwork.Scan(terminal), whoAmI);
		}

		private static void HandleWithdraw(Point terminal, int type, int whoAmI)
		{
			if (Allowed(terminal, whoAmI, lastWithdraw))
				ModContent.GetInstance<TerminalSystem>().Dispense(terminal, type, Main.player[whoAmI]);
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
			int count = reader.ReadInt32();

			if (first || pending == null)
				pending = new StorageSnapshot { ChestCount = chestCount };

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
