using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using RecipeIndex.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace RecipeIndex.Content.Storage
{
	// Multiplayer: clients don't know the contents of chests they haven't opened,
	// so they ask the server to scan, and it replies with what is in the network and where.
	public static class TerminalNet
	{
		private enum MessageType : byte
		{
			RequestContents,
			Contents,
		}

		// A ModPacket can't exceed ~65 KB; large networks are sent in several chunks.
		private const int ChunkBudget = 30000;
		private const float MaxRequestDistance = 16 * 20;
		private const int MinTicksBetweenRequests = 10;

		private static Mod Mod => ModContent.GetInstance<TerminalSystem>().Mod;

		private static readonly uint[] lastRequest = new uint[Main.maxPlayers];
		private static StorageSnapshot pending;

		public static void SendRequest(Point terminal)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)MessageType.RequestContents);
			WritePoint(packet, terminal);
			packet.Send();
		}

		public static void Handle(BinaryReader reader, int whoAmI)
		{
			switch ((MessageType)reader.ReadByte()) {
				case MessageType.RequestContents:
					HandleRequest(ReadPoint(reader), whoAmI);
					break;
				case MessageType.Contents:
					HandleContents(reader);
					break;
			}
		}

		private static void HandleRequest(Point terminal, int whoAmI)
		{
			if (Main.netMode != NetmodeID.Server)
				return;

			// Basic checks so a client can't snoop on distant networks or flood the server.
			Tile tile = Main.tile[terminal.X, terminal.Y];
			var center = new Vector2(terminal.X * 16 + 16, terminal.Y * 16 + 16);
			if (!tile.HasTile || tile.TileType != ModContent.TileType<TerminalTile>() || Main.player[whoAmI].Distance(center) > MaxRequestDistance)
				return;
			if (Main.GameUpdateCount - lastRequest[whoAmI] < MinTicksBetweenRequests)
				return;
			lastRequest[whoAmI] = Main.GameUpdateCount;

			SendContents(terminal, StorageNetwork.Scan(terminal), whoAmI);
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
