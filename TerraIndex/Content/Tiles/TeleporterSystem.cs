using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TerraIndex.Content.Storage;
using TerraIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace TerraIndex.Content.Tiles
{
	// Wireless teleporters' channels (each teleporter's top-left tile -> its channel, from 1). A teleporter takes you to
	// the next one on its channel, so breaking one leaves the others on it as they were. A channel exists while some
	// teleporter is on it. Saved with the world. The server (or single player) owns the channels and sends them to
	// every client with the world data, so clients know where a teleporter goes even when the others are too far away
	// for them to have their tiles. The Teleporter Linker opens the channel menu (TeleporterChannelState).
	public class TeleporterSystem : ModSystem
	{
		// Sent instead of a channel: a new one (the server picks its number) or none.
		public const int NewChannel = -1;
		public const int NoChannel = 0;

		// The menu closes when the player gets this far from the teleporter.
		private const float MenuReach = 12 * 16;

		private readonly Dictionary<Point, int> channels = new();

		private UserInterface ui;
		private GameTime lastGameTime;

		// The teleporter the channel menu is open for.
		public Point MenuTeleporter { get; private set; }

		public int ChannelOf(Point teleporter) => channels.TryGetValue(teleporter, out int channel) ? channel : NoChannel;

		// The channels in use, with how many teleporters each has.
		public List<(int Channel, int Count)> Channels() =>
			channels.Values.GroupBy(channel => channel).Select(group => (group.Key, group.Count())).OrderBy(entry => entry.Key).ToList();

		// The teleporter this one takes you to: the next one on its channel, in position order, so several on a
		// channel go round in a loop. False if it has no channel or is alone on it.
		public bool TryGetDestination(Point teleporter, out Point destination)
		{
			destination = default;
			int channel = ChannelOf(teleporter);
			if (channel == NoChannel)
				return false;
			List<Point> others = channels.Where(entry => entry.Value == channel && entry.Key != teleporter).Select(entry => entry.Key)
				.OrderBy(point => point.X).ThenBy(point => point.Y).ToList();
			if (others.Count == 0)
				return false;
			destination = others.FirstOrDefault(point => point.X > teleporter.X || (point.X == teleporter.X && point.Y > teleporter.Y), others[0]);
			return true;
		}

		public override void Load()
		{
			if (!Main.dedServ)
				ui = new UserInterface();
		}

		public override void OnWorldLoad() => channels.Clear();

		public override void OnWorldUnload()
		{
			channels.Clear();
			ui?.SetState(null);
		}

		public void OpenMenu(Point teleporter)
		{
			MenuTeleporter = teleporter;
			ui.SetState(UIHelpers.Build(new TeleporterChannelState()));
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void CloseMenu()
		{
			if (ui?.CurrentState == null)
				return;
			ui.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		// From the channel menu: asks the server, or sets it right away in single player.
		public void RequestChannel(Point teleporter, int channel)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TerminalNet.SendTeleporterChannel(teleporter, channel);
			else
				SetChannel(teleporter, channel);
		}

		// Single player or server. Returns the channel it ended up on.
		public int SetChannel(Point teleporter, int channel)
		{
			if (!WirelessTeleporterTile.IsTeleporter(teleporter))
				return NoChannel;
			if (channel == NewChannel)
				channel = channels.Count == 0 ? 1 : channels.Values.Max() + 1;
			if (channel <= NoChannel)
				channels.Remove(teleporter);
			else
				channels[teleporter] = channel;
			Sync();
			return ChannelOf(teleporter);
		}

		// Single player or server, when a teleporter is broken. The others keep their channel.
		public void Remove(Point teleporter)
		{
			if (channels.Remove(teleporter))
				Sync();
		}

		private static void Sync()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			lastGameTime = gameTime;
			if (ui?.CurrentState == null)
				return;
			Player player = Main.LocalPlayer;
			bool escape = Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape);
			if (escape || player.dead || !WirelessTeleporterTile.IsTeleporter(MenuTeleporter)
				|| player.Distance(WirelessTeleporterTile.Center(MenuTeleporter)) > MenuReach) {
				CloseMenu();
				return;
			}
			ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index == -1)
				return;
			layers.Insert(index, UIHelpers.Layer("TerraIndex: Teleporter Channels", () => {
				if (ui?.CurrentState != null && lastGameTime != null)
					ui.Draw(Main.spriteBatch, lastGameTime);
				return true;
			}));
		}

		public override void SaveWorldData(TagCompound tag)
		{
			if (channels.Count > 0)
				tag["teleporterChannels"] = channels.SelectMany(entry => new[] { entry.Key.X, entry.Key.Y, entry.Value }).ToArray();
		}

		public override void LoadWorldData(TagCompound tag)
		{
			channels.Clear();
			int[] values = tag.GetIntArray("teleporterChannels");
			for (int k = 0; k + 2 < values.Length; k += 3)
				channels[new Point(values[k], values[k + 1])] = values[k + 2];

			// Worlds from before channels kept linked pairs: each pair gets a channel of its own.
			int[] pairs = tag.GetIntArray("teleporterLinks");
			int next = channels.Count == 0 ? 1 : channels.Values.Max() + 1;
			for (int k = 0; k + 3 < pairs.Length; k += 4, next++) {
				channels[new Point(pairs[k], pairs[k + 1])] = next;
				channels[new Point(pairs[k + 2], pairs[k + 3])] = next;
			}
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(channels.Count);
			foreach (var (point, channel) in channels) {
				writer.Write(point.X);
				writer.Write(point.Y);
				writer.Write(channel);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			channels.Clear();
			for (int count = reader.ReadInt32(); count > 0; count--) {
				var point = new Point(reader.ReadInt32(), reader.ReadInt32());
				channels[point] = reader.ReadInt32();
			}
		}
	}
}
