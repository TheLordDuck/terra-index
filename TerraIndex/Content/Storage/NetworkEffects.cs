using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using TerraIndex.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Storage
{
	// Client only, just for show: delivery drones carrying items between the chests and the terminal's door, and sparks
	// and a sound on the chests items go into or out of (both can be turned off in ClientConfig).
	// Taken out: the chest sparks and a drone flies the item to the door, arriving as the door finishes opening
	// (TerminalSystem.DeliveryTicks), or straight to the player when they aren't at the door. Put in: the items fly
	// from the player into the door first, then drones take them to the chests, which spark when they arrive.
	public class NetworkEffects : ModSystem
	{
		public const int FlightTicks = 24;
		// Chests farther than this from the door get no drone (it would cross the screen in a blink), only the sparks.
		private const float DroneRange = 16 * 60;
		// Moves this far from the player are off screen.
		private const float ShowRange = 16 * 120;
		private const int GlowTicks = 20;
		// DeliveryDrone.png: two frames stacked vertically (the rotors spinning).
		private const int FrameHeight = 14;

		private class Drone
		{
			public Vector2 From;
			public Vector2 To;
			// Flying to this player (following them as they move) instead of to To; -1 = none.
			public int Player = -1;
			public int Type;
			public Point Chest;
			public bool Entering;
			// Without drones (config) it is only a timer for the chest's sparks.
			public bool Visible;
			public int Delay;
			public int Age;
		}

		private readonly List<Drone> drones = new();
		// Chest top-left corner -> ticks left of its glow.
		private readonly Dictionary<Point, int> glows = new();
		private Asset<Texture2D> texture;

		public override void Load()
		{
			if (!Main.dedServ)
				texture = ModContent.Request<Texture2D>("TerraIndex/Content/Storage/DeliveryDrone");
		}

		public override void OnWorldUnload()
		{
			drones.Clear();
			glows.Clear();
		}

		// toPlayer: who the items taken out go to, or -1 for the terminal's door.
		public void Show(Point terminal, IEnumerable<ChestMove> moves, int toPlayer = -1)
		{
			if (Main.dedServ)
				return;
			Vector2 door = TerminalSystem.DoorCenter(terminal);
			Player player = Main.LocalPlayer;
			foreach (ChestMove move in moves) {
				Vector2 chest = ChestCenter(move.Chest);
				if (player.Distance(chest) > ShowRange && player.Distance(door) > ShowRange)
					continue;
				int receiver = move.Entering ? -1 : toPlayer;
				Vector2 target = receiver >= 0 ? Main.player[receiver].Center : move.Entering ? chest : door;
				// A little spread, so several drones leaving together don't fly on top of each other.
				Vector2 spread = Main.rand.NextVector2Circular(6, 4);
				drones.Add(new Drone {
					From = (move.Entering ? door : chest) + spread,
					To = target + spread,
					Player = receiver,
					Type = move.Type,
					Chest = move.Chest,
					Entering = move.Entering,
					Visible = ClientConfig.Instance.DeliveryDrones && Vector2.Distance(move.Entering ? door : chest, target) <= DroneRange,
					Delay = move.Entering ? TerminalSystem.FlyTicks : 0,
				});
			}
		}

		private static Vector2 ChestCenter(Point chest) => new(chest.X * 16 + ChestWidth(chest) * 8, chest.Y * 16 + 16);

		// Chests are 2 tiles wide; dressers are 3.
		private static int ChestWidth(Point chest) => TileID.Sets.BasicDresser[Main.tile[chest.X, chest.Y].TileType] ? 3 : 2;

		// One burst per chest, even when several items go in or out of it at once.
		private void Spark(Point chest, bool entering)
		{
			if (!ClientConfig.Instance.ChestEffects || glows.ContainsKey(chest))
				return;
			glows[chest] = GlowTicks;

			Vector2 center = ChestCenter(chest);
			for (int i = 0; i < 10; i++) {
				Vector2 offset = Main.rand.NextVector2Circular(14, 10);
				// Out: the sparks burst outwards. In: they appear around the chest and fall into it.
				Vector2 position = center + offset * (entering ? 1.6f : 0.3f);
				Dust dust = Dust.NewDustPerfect(position, DustID.Electric, offset * (entering ? -0.12f : 0.15f), Scale: 0.6f);
				dust.noGravity = true;
			}
			SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.35f, Pitch = entering ? -0.2f : 0.3f }, center);
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ)
				return;

			for (int i = drones.Count - 1; i >= 0; i--) {
				Drone drone = drones[i];
				if (drone.Delay > 0) {
					drone.Delay--;
					continue;
				}
				if (drone.Age++ == 0 && !drone.Entering)
					Spark(drone.Chest, false);
				if (drone.Visible)
					Lighting.AddLight(Position(drone), 0.1f, 0.35f, 0.45f);
				if (drone.Age >= FlightTicks) {
					if (drone.Entering)
						Spark(drone.Chest, true);
					drones.RemoveAt(i);
				}
			}

			foreach (Point chest in glows.Keys.ToList()) {
				int ticks = --glows[chest];
				if (ticks <= 0)
					glows.Remove(chest);
				else
					Lighting.AddLight(ChestCenter(chest), new Vector3(0.2f, 0.6f, 0.8f) * (ticks / (float)GlowTicks));
			}
		}

		private static Vector2 Target(Drone drone) => drone.Player >= 0 ? Main.player[drone.Player].Center : drone.To;

		// Eased along the line, with a small hop, like the items flying into the door.
		private static Vector2 Position(Drone drone)
		{
			float t = drone.Age / (float)FlightTicks;
			float eased = t * t * (3 - 2 * t);
			return Vector2.Lerp(drone.From, Target(drone), eased) - new Vector2(0, MathF.Sin(t * MathF.PI) * 24f);
		}

		public override void PostDrawTiles()
		{
			if (!drones.Any(drone => drone.Visible && drone.Delay == 0))
				return;

			SpriteBatch spriteBatch = Main.spriteBatch;
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			Texture2D sprite = texture.Value;
			var source = new Rectangle(0, (int)(Main.GameUpdateCount / 3 % 2) * FrameHeight, sprite.Width, FrameHeight);
			foreach (Drone drone in drones) {
				if (!drone.Visible || drone.Delay > 0)
					continue;

				Vector2 position = Position(drone) - Main.screenPosition;
				float t = drone.Age / (float)FlightTicks;
				// Fades in leaving the chest (or the door) and out arriving.
				float alpha = Math.Min(1f, Math.Min(t, 1f - t) * 6f);
				// Its eye glows a little, so it can be seen in the dark.
				Color light = Color.Lerp(Lighting.GetColor((int)(position.X + Main.screenPosition.X) / 16, (int)(position.Y + Main.screenPosition.Y) / 16), Color.White, 0.3f) * alpha;
				// The sprite looks right.
				SpriteEffects flip = Target(drone).X < drone.From.X ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
				spriteBatch.Draw(sprite, position, source, light, 0f, new Vector2(sprite.Width / 2f, FrameHeight / 2f), 1f, flip, 0f);

				// The item hangs from the claw.
				Main.instance.LoadItem(drone.Type);
				Main.GetItemDrawFrame(drone.Type, out Texture2D itemTexture, out Rectangle frame);
				float scale = Math.Min(1f, 14f / Math.Max(frame.Width, frame.Height));
				var itemPosition = position + new Vector2(0, FrameHeight / 2f + 1 + frame.Height * scale / 2f);
				spriteBatch.Draw(itemTexture, itemPosition, frame, light, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
			}

			spriteBatch.End();
		}
	}
}
