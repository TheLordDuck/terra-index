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
	// Plays the radio clips, in a shuffled order, at random moments: sometimes a few seconds apart, sometimes a couple of
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

		// The clips and their lengths in seconds (the server can't load sounds to ask how long they are): the user's first
		// four, then the 31 phrases of Verne's "Las mejores frases de Rajoy durante su mandato", cut where the video's
		// background changes.
		private static readonly (string Name, float Seconds)[] ClipList = {
			("RadioSiu", 6.7f), ("RadioLosDos", 18.3f), ("RadioSi", 5.4f), ("RadioNo", 5f),
			("RadioRajoyAlicante", 4.57f), ("RadioRajoyAplauden", 12.23f), ("RadioRajoyTomaDemocracia", 2.59f),
			("RadioRajoyTalavera", 7.04f), ("RadioRajoyCuantoPeor", 8.73f), ("RadioRajoyVeryDifficult", 4.02f),
			("RadioRajoyPiensanAntes", 4.23f), ("RadioRajoyMuyEspanoles", 6.98f), ("RadioRajoySentimientos", 2.57f),
			("RadioRajoyImposible", 10.48f), ("RadioRajoyLaSegunda", 2.15f), ("RadioRajoyEnganar", 6.86f),
			("RadioRajoyNoHeDormido", 3.80f), ("RadioRajoyAviones", 5.79f), ("RadioRajoyGalileo", 5.30f),
			("RadioRajoySerio", 3.26f), ("RadioRajoyAlcalde", 7.11f), ("RadioRajoyEuropea", 1.65f),
			("RadioRajoyPonencias", 5.31f), ("RadioRajoyPodemos", 5.72f), ("RadioRajoyGranNacion", 5.82f),
			("RadioRajoy75Millones", 4.90f), ("RadioRajoyUnVaso", 2.90f), ("RadioRajoyMaquinas", 6.74f),
			("RadioRajoyCosasBonitas", 6.48f), ("RadioRajoyHacenCosas", 2.00f), ("RadioRajoyViajes", 4.72f),
			("RadioRajoyAguaDelCielo", 4.52f), ("RadioRajoySalvoAlgunaCosa", 8.62f), ("RadioRajoyLaEPA", 4.08f),
			("RadioRajoyFinDeLaCita", 2.08f),
		};
		private static readonly SoundStyle[] Clips = ClipList.Select(clip => new SoundStyle("TerraIndex/Content/Sounds/" + clip.Name) { Volume = 0.8f, MaxInstances = 1 }).ToArray();
		private static readonly int[] ClipTicks = ClipList.Select(clip => (int)System.Math.Ceiling(clip.Seconds) * 60).ToArray();

		// Server or single player: the schedule.
		private readonly List<Point> radios = new();
		// The clips left to play, in a random order: every clip plays once before any repeats.
		private readonly List<int> order = new();
		private int last = -1;
		private int timer;
		private int scanTimer;

		// Every client: the clip playing now.
		private Point playingAt;
		private SlotId playing = SlotId.Invalid;

		public override void OnWorldLoad()
		{
			radios.Clear();
			playing = SlotId.Invalid;
			order.Clear();
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

			int clip = NextClip();
			// The clip's length plus the silence after it.
			timer = ClipTicks[clip] + Gap();

			if (Main.netMode == NetmodeID.Server)
				TerminalNet.SendRadioPlay(clip, radios.Take(MaxRadios).ToList());
			else
				Play(clip, radios);
		}

		private int NextClip()
		{
			if (order.Count == 0) {
				order.AddRange(Enumerable.Range(0, Clips.Length).OrderBy(_ => Main.rand.Next()));
				// Shuffling doesn't make the last clip the first of the next round.
				if (order.Count > 1 && order[0] == last)
					(order[0], order[^1]) = (order[^1], order[0]);
			}
			last = order[0];
			order.RemoveAt(0);
			return last;
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
