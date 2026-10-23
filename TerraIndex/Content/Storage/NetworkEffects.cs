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
	// Client only, just for show: when items are taken out, the chests they came from spark, with a cyan glow and a
	// sound (can be turned off in ClientConfig). With a portable terminal far from its door, a delivery drone does the
	// carrying: it comes down from the sky and hovers above the player. Taking out, it lets the item go (the real item
	// is spawned there by the server at ReleaseTicks and falls onto them); putting in, the items fly up into its claw
	// one after another. Then it flies off upwards.
	public class NetworkEffects : ModSystem
	{
		// Chests this far from the player are off screen.
		private const float ShowRange = 16 * 120;
		private const int GlowTicks = 20;

		// The drone: coming down, hovering until it lets the item go, hovering a little more, flying off.
		public const int FlightTicks = 24;
		public const int ReleaseTicks = FlightTicks + 4;
		private const int HoverTicks = 14;
		private const int LeaveTicks = 18;
		// Putting in: how long each item takes to fly up into the claw, and the pause after the last one.
		private const int CatchTicks = 16;
		private const int CaughtHoverTicks = 8;
		// How high above the player's center it hovers, and how high it starts.
		private const float HoverHeight = 16 * 5;
		private const float ArriveHeight = 16 * 22;
		// DeliveryDrone.png: two frames stacked vertically (the rotors spinning).
		private const int FrameHeight = 14;

		private class Drone
		{
			public int Player;
			// Taking out: the item it brings.
			public int Type;
			// Putting in: the items flying up into it, one every Stagger ticks once it has arrived. Null when taking out.
			public List<int> Catching;
			public int Stagger;
			// Horizontal offset, so several drones arriving together don't overlap.
			public float Offset;
			public int Age;

			public int CatchStart(int i) => FlightTicks + i * Stagger;
			public int LeaveAt => Catching == null ? ReleaseTicks + HoverTicks : CatchStart(Catching.Count - 1) + CatchTicks + CaughtHoverTicks;
			public int Lifetime => LeaveAt + LeaveTicks;

			// The item hanging from the claw: the one it brings until it lets it go, or the last one it caught.
			public int Carried()
			{
				if (Catching == null)
					return Age < ReleaseTicks ? Type : 0;
				for (int i = Catching.Count - 1; i >= 0; i--) {
					if (Age >= CatchStart(i) + CatchTicks)
						return Catching[i];
				}
				return 0;
			}
		}

		// Chest top-left corner -> ticks left of its glow.
		private readonly Dictionary<Point, int> glows = new();
		private readonly List<Drone> drones = new();
		private Asset<Texture2D> texture;

		public override void Load()
		{
			if (!Main.dedServ)
				texture = ModContent.Request<Texture2D>("TerraIndex/Content/Storage/DeliveryDrone");
		}

		public override void OnWorldUnload()
		{
			glows.Clear();
			drones.Clear();
		}

		public void Show(IEnumerable<ChestMove> moves)
		{
			if (Main.dedServ || !ClientConfig.Instance.ChestEffects)
				return;
			foreach (ChestMove move in moves) {
				if (Main.LocalPlayer.Distance(ChestCenter(move.Chest)) <= ShowRange)
					Spark(move.Chest);
			}
		}

		// A drone bringing an item of this type to this player.
		public void ShowDrone(int player, int type)
		{
			if (Main.dedServ || Main.LocalPlayer.Distance(Main.player[player].Center) > ShowRange)
				return;
			drones.Add(new Drone { Player = player, Type = type, Offset = Main.rand.NextFloat(-10f, 10f) });
		}

		// A drone taking these items from this player.
		public void ShowPickupDrone(int player, List<int> types)
		{
			if (Main.dedServ || types.Count == 0 || Main.LocalPlayer.Distance(Main.player[player].Center) > ShowRange)
				return;
			drones.Add(new Drone {
				Player = player,
				Catching = types,
				Stagger = Math.Clamp(40 / types.Count, 1, 4),
				Offset = Main.rand.NextFloat(-10f, 10f),
			});
		}

		// Single player or server: where the item a drone brings is spawned, just under its claw. Under a low ceiling,
		// on the player instead.
		public static Vector2 DropPoint(Player player)
		{
			Vector2 position = player.Center - new Vector2(0, HoverHeight - FrameHeight);
			if (Collision.SolidCollision(position - new Vector2(8), 16, 16) || !Collision.CanHitLine(position, 1, 1, player.Center, 1, 1))
				return player.Center;
			return position;
		}

		private static Vector2 ChestCenter(Point chest) => new(chest.X * 16 + ChestWidth(chest) * 8, chest.Y * 16 + 16);

		// Chests are 2 tiles wide; dressers are 3.
		private static int ChestWidth(Point chest) => TileID.Sets.BasicDresser[Main.tile[chest.X, chest.Y].TileType] ? 3 : 2;

		// One burst per chest, even when several items go out of it at once.
		private void Spark(Point chest)
		{
			if (glows.ContainsKey(chest))
				return;
			glows[chest] = GlowTicks;

			Vector2 center = ChestCenter(chest);
			for (int i = 0; i < 10; i++) {
				Vector2 offset = Main.rand.NextVector2Circular(14, 10);
				// The sparks burst outwards.
				Dust dust = Dust.NewDustPerfect(center + offset * 0.3f, DustID.Electric, offset * 0.15f, Scale: 0.6f);
				dust.noGravity = true;
			}
			SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.35f, Pitch = 0.3f }, center);
		}

		// The drone lets an item go, or catches one: a few sparks at the claw and a click.
		private static void ClawSparks(Drone drone, float pitch)
		{
			Vector2 claw = Claw(drone);
			for (int i = 0; i < 8; i++) {
				Dust dust = Dust.NewDustPerfect(claw, DustID.Electric, Main.rand.NextVector2Circular(1.5f, 1f) + new Vector2(0, 0.8f), Scale: 0.5f);
				dust.noGravity = true;
			}
			SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.3f, Pitch = pitch }, claw);
		}

		public override void PostUpdateEverything()
		{
			if (Main.dedServ)
				return;

			foreach (Point chest in glows.Keys.ToList()) {
				int ticks = --glows[chest];
				if (ticks <= 0)
					glows.Remove(chest);
				else
					Lighting.AddLight(ChestCenter(chest), new Vector3(0.2f, 0.6f, 0.8f) * (ticks / (float)GlowTicks));
			}

			for (int i = drones.Count - 1; i >= 0; i--) {
				Drone drone = drones[i];
				drone.Age++;
				// Its own light, so it can be seen in caves and at night.
				Lighting.AddLight(Position(drone), new Vector3(0.3f, 0.7f, 0.95f) * Pulse(drone) * Alpha(drone));
				if (drone.Catching == null) {
					if (drone.Age == ReleaseTicks)
						ClawSparks(drone, 0.6f);
				}
				else {
					// A click for each item caught, but only sparks for the first and the last so a big deposit isn't a mess.
					for (int item = 0; item < drone.Catching.Count; item++) {
						if (drone.Age != drone.CatchStart(item) + CatchTicks)
							continue;
						if (item == 0 || item == drone.Catching.Count - 1)
							ClawSparks(drone, 0.2f);
						else
							SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.2f, Pitch = 0.2f }, Claw(drone));
					}
				}
				if (drone.Age >= drone.Lifetime || !Main.player[drone.Player].active)
					drones.RemoveAt(i);
			}
		}

		// Above the player (following them), coming down eased, bobbing while it hovers, then flying off speeding up.
		private static Vector2 Position(Drone drone)
		{
			Vector2 hover = Main.player[drone.Player].Center + new Vector2(drone.Offset, -HoverHeight);
			if (drone.Age < FlightTicks) {
				float t = drone.Age / (float)FlightTicks;
				float eased = 1f - (1f - t) * (1f - t);
				return hover - new Vector2(0, (ArriveHeight - HoverHeight) * (1f - eased));
			}
			hover.Y += MathF.Sin(drone.Age * 0.25f) * 2f;
			int leaving = drone.Age - drone.LeaveAt;
			if (leaving > 0)
				hover.Y -= leaving * leaving * 0.6f;
			return hover;
		}

		private static Vector2 Claw(Drone drone) => Position(drone) + new Vector2(0, FrameHeight / 2f);

		private static float Alpha(Drone drone) => Math.Clamp(Math.Min(drone.Age / 4f, (drone.Lifetime - drone.Age) / 6f), 0f, 1f);

		private static float Pulse(Drone drone) => 0.85f + 0.15f * MathF.Sin(drone.Age * 0.3f);

		private static void DrawItem(SpriteBatch spriteBatch, int type, Vector2 center, float size, Color color)
		{
			Main.instance.LoadItem(type);
			Main.GetItemDrawFrame(type, out Texture2D itemTexture, out Rectangle frame);
			float scale = Math.Min(1f, size / Math.Max(frame.Width, frame.Height));
			spriteBatch.Draw(itemTexture, center, frame, color, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
		}

		public override void PostDrawTiles()
		{
			if (drones.Count == 0)
				return;

			SpriteBatch spriteBatch = Main.spriteBatch;
			Texture2D sprite = texture.Value;
			var source = new Rectangle(0, (int)(Main.GameUpdateCount / 3 % 2) * FrameHeight, sprite.Width, FrameHeight);
			var origin = new Vector2(sprite.Width / 2f, FrameHeight / 2f);

			// A soft cyan halo behind each drone, drawn additively so it glows.
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			foreach (Drone drone in drones) {
				Vector2 position = Position(drone) - Main.screenPosition;
				Color glow = new Color(80, 200, 255) * (0.45f * Pulse(drone) * Alpha(drone));
				for (int i = 0; i < 4; i++)
					spriteBatch.Draw(sprite, position + new Vector2(2, 0).RotatedBy(MathHelper.PiOver2 * i), source, glow, 0f, origin, 1.15f, SpriteEffects.None, 0f);
			}
			spriteBatch.End();

			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
			foreach (Drone drone in drones) {
				Vector2 position = Position(drone) - Main.screenPosition;
				// Lit by itself: drawn at full brightness, whatever the light around it.
				Color light = Color.White * Alpha(drone);
				spriteBatch.Draw(sprite, position, source, light, 0f, origin, 1f, SpriteEffects.None, 0f);

				// Putting in: the items flying up from the player into the claw, in an arc, shrinking as they arrive.
				if (drone.Catching != null) {
					Vector2 from = Main.player[drone.Player].Center - Main.screenPosition;
					Vector2 claw = Claw(drone) - Main.screenPosition;
					for (int i = 0; i < drone.Catching.Count; i++) {
						float t = (drone.Age - drone.CatchStart(i)) / (float)CatchTicks;
						if (t < 0f || t >= 1f)
							continue;
						Vector2 itemPosition = Vector2.Lerp(from, claw, t * t) + new Vector2(MathF.Sin(t * MathF.PI) * 14f * (i % 2 == 0 ? 1 : -1), 0);
						Color itemLight = Lighting.GetColor((int)((itemPosition.X + Main.screenPosition.X) / 16), (int)((itemPosition.Y + Main.screenPosition.Y) / 16));
						DrawItem(spriteBatch, drone.Catching[i], itemPosition, 24f * (1f - 0.4f * t), itemLight);
					}
				}

				// The item hanging from the claw.
				int carried = drone.Carried();
				if (carried > 0)
					DrawItem(spriteBatch, carried, position + new Vector2(0, FrameHeight / 2f + 8), 14f, light);
			}
			spriteBatch.End();
		}
	}
}
