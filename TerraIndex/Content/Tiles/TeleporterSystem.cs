using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TerraIndex.Content.Tiles
{
	// Which wireless teleporter goes to which, in pairs (each one's top-left tile). Saved with the world. The server
	// (or single player) owns the links and sends them to every client with the world data, so clients know where a
	// teleporter goes even when its partner is too far away for them to have its tiles.
	public class TeleporterSystem : ModSystem
	{
		private readonly Dictionary<Point, Point> links = new();

		public bool TryGetPartner(Point teleporter, out Point partner) => links.TryGetValue(teleporter, out partner);

		public override void OnWorldLoad() => links.Clear();

		public override void OnWorldUnload() => links.Clear();

		// From the Teleporter Linker: asks the server, or links them right away in single player.
		public void RequestLink(Point a, Point b)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TerminalNet.SendTeleporterLink(a, b);
			else
				Link(a, b);
		}

		// Single player or server. Their old partners are left unlinked.
		public void Link(Point a, Point b)
		{
			if (a == b || !WirelessTeleporterTile.IsTeleporter(a) || !WirelessTeleporterTile.IsTeleporter(b))
				return;
			Remove(a);
			Remove(b);
			links[a] = b;
			links[b] = a;
			Sync();
		}

		// Single player or server, when a teleporter is broken.
		public void Unlink(Point teleporter)
		{
			if (Remove(teleporter))
				Sync();
		}

		private bool Remove(Point teleporter)
		{
			if (!links.Remove(teleporter, out Point partner))
				return false;
			links.Remove(partner);
			return true;
		}

		private static void Sync()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		// Each pair once.
		private IEnumerable<(Point A, Point B)> Pairs => links.Where(link => link.Key.X < link.Value.X || (link.Key.X == link.Value.X && link.Key.Y < link.Value.Y))
			.Select(link => (link.Key, link.Value));

		public override void SaveWorldData(TagCompound tag)
		{
			if (links.Count > 0)
				tag["teleporterLinks"] = Pairs.SelectMany(pair => new[] { pair.A.X, pair.A.Y, pair.B.X, pair.B.Y }).ToArray();
		}

		public override void LoadWorldData(TagCompound tag)
		{
			links.Clear();
			int[] values = tag.GetIntArray("teleporterLinks");
			for (int k = 0; k + 3 < values.Length; k += 4) {
				var a = new Point(values[k], values[k + 1]);
				var b = new Point(values[k + 2], values[k + 3]);
				links[a] = b;
				links[b] = a;
			}
		}

		public override void NetSend(BinaryWriter writer)
		{
			List<(Point A, Point B)> pairs = Pairs.ToList();
			writer.Write(pairs.Count);
			foreach (var (a, b) in pairs) {
				writer.Write(a.X);
				writer.Write(a.Y);
				writer.Write(b.X);
				writer.Write(b.Y);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			links.Clear();
			for (int count = reader.ReadInt32(); count > 0; count--) {
				var a = new Point(reader.ReadInt32(), reader.ReadInt32());
				var b = new Point(reader.ReadInt32(), reader.ReadInt32());
				links[a] = b;
				links[b] = a;
			}
		}
	}
}
