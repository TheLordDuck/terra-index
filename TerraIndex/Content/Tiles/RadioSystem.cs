using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using TerraIndex.Content.Storage;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Tiles
{
	// Plays the radio clips, alternating them, at random moments: sometimes a few seconds apart, sometimes a couple of
	// minutes of silence. The server (or the single player) decides when and which clip, and sends the radios to play
	// at (each player's nearest one that is on), so everyone hears the same clip at the same time. Each client then
	// plays it once, at the nearest of those radios, so several radios don't talk over each other.
	public class RadioSystem : ModSystem
	{
		private const int ScanInterval = 30;
		private const int RangeX = 60;
		private const int RangeY = 40;
		private const int MaxRadios = 255;
		// One note every ~20 ticks on average while a clip plays.
		private const int NoteChance = 20;

		private static readonly SoundStyle[] Clips = {
			new("TerraIndex/Content/Sounds/RadioSiu") { Volume = 0.8f, MaxInstances = 1 },
			new("TerraIndex/Content/Sounds/RadioLosDos") { Volume = 0.8f, MaxInstances = 1 },
			new("TerraIndex/Content/Sounds/RadioSi") { Volume = 0.8f, MaxInstances = 1 },
			new("TerraIndex/Content/Sounds/RadioNo") { Volume = 0.8f, MaxInstances = 1 },
		};
		// The server can't load sounds to ask how long they are: 6.7 s, 18.3 s, 5.4 s and 5 s, rounded up.
		private static readonly int[] ClipTicks = { 7 * 60, 19 * 60, 6 * 60, 5 * 60 };

		// Server or single player: the schedule.
		private readonly List<Point> radios = new();
		private int next;
		private int timer;
		private int scanTimer;

		// Every client: the clip playing now.
		private Point playingAt;
		private SlotId playing = SlotId.Invalid;

		public override void OnWorldLoad()
		{
			radios.Clear();
			playing = SlotId.Invalid;
			next = Main.rand.Next(Clips.Length);
		}

		public override void PostUpdateEverything()
		{
			// Turned off or broken while playing: it goes quiet at once. Otherwise music notes float out of it.
			if (!Main.dedServ && SoundEngine.TryGetActiveSound(playing, out ActiveSound sound)) {
				if (!RadioTile.IsOn(playingAt))
					sound.Stop();
				else if (Main.rand.NextBool(NoteChance))
					SpawnNote(playingAt);
			}

			if (Main.netMode != NetmodeID.MultiplayerClient)
				UpdateSchedule();
		}

		private void UpdateSchedule()
		{
			if (--scanTimer <= 0) {
				scanTimer = ScanInterval;
				bool hadRadios = radios.Count > 0;
				FindRadios();
				// A radio just placed, turned on or come into range starts after a short while.
				if (!hadRadios && radios.Count > 0)
					timer = Seconds(3, 15);
			}

			if (radios.Count == 0 || --timer > 0)
				return;

			int clip = next;
			next = (next + 1) % Clips.Length;
			// The clip's length plus the silence after it.
			timer = ClipTicks[clip] + Gap();

			if (Main.netMode == NetmodeID.Server)
				TerminalNet.SendRadioPlay(clip, radios.Take(MaxRadios).ToList());
			else
				Play(clip, radios);
		}

		// Every client: plays the clip at the nearest of the radios, if any is close enough to hear.
		public void Play(int clip, List<Point> at)
		{
			if (Main.dedServ || clip < 0 || clip >= Clips.Length)
				return;
			Vector2 player = Main.LocalPlayer.Center;
			Point? nearest = null;
			float best = float.MaxValue;
			foreach (Point radio in at) {
				Vector2 center = RadioTile.Center(radio);
				float distance = Vector2.DistanceSquared(center, player);
				if (!InRange(center, player) || distance >= best || !WorldGen.InWorld(radio.X, radio.Y) || !RadioTile.IsOn(radio))
					continue;
				best = distance;
				nearest = radio;
			}
			if (nearest is not Point radioAt)
				return;

			if (SoundEngine.TryGetActiveSound(playing, out ActiveSound old))
				old.Stop();
			playingAt = radioAt;
			playing = SoundEngine.PlaySound(Clips[clip], RadioTile.Center(radioAt));
		}

		// The music box's notes (gores 570-572), coming out by the antenna and drifting up.
		private static void SpawnNote(Point radio)
		{
			var position = new Vector2(radio.X * 16 + 22, radio.Y * 16 - 10);
			var velocity = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.6f, 1.2f));
			int gore = Gore.NewGore(new EntitySource_TileUpdate(radio.X, radio.Y), position, velocity, Main.rand.Next(570, 573), 0.8f);
			Main.gore[gore].velocity = velocity;
		}

		private static bool InRange(Vector2 radio, Vector2 player)
		{
			Vector2 offset = radio - player;
			return System.Math.Abs(offset.X) <= RangeX * 16 && System.Math.Abs(offset.Y) <= RangeY * 16;
		}

		// Each player's nearest radio that is on (players close together share one).
		private void FindRadios()
		{
			radios.Clear();
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (!player.active)
					continue;
				if (FindNearest(player) is Point radio && !radios.Contains(radio))
					radios.Add(radio);
			}
		}

		private static Point? FindNearest(Player player)
		{
			Point center = player.Center.ToTileCoordinates();
			Point? best = null;
			float bestDistance = float.MaxValue;
			for (int x = center.X - RangeX; x <= center.X + RangeX; x++) {
				for (int y = center.Y - RangeY; y <= center.Y + RangeY; y++) {
					if (!WorldGen.InWorld(x, y))
						continue;
					// Only the left half counts, so each radio is found once.
					var point = new Point(x, y);
					if (!RadioTile.IsOn(point) || RadioTile.TopLeft(x, y) != point)
						continue;
					float distance = Vector2.DistanceSquared(RadioTile.Center(point), player.Center);
					if (distance < bestDistance) {
						bestDistance = distance;
						best = point;
					}
				}
			}
			return best;
		}

		// Mostly under a minute and a half, now and then up to two and a half, so it's hard to guess.
		private static int Gap()
		{
			int roll = Main.rand.Next(100);
			if (roll < 35)
				return Seconds(10, 40);
			if (roll < 80)
				return Seconds(40, 90);
			return Seconds(90, 150);
		}

		private static int Seconds(int min, int max) => Main.rand.Next(min * 60, max * 60 + 1);
	}
}
