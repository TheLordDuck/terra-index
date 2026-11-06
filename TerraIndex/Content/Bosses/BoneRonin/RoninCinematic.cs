using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The Bone Ronin's cinematics: his entrance and both phase changes (the 3-second power-up, see BoneRonin.PowerUp).
	// The camera glides to him and zooms in, black bars close in from the top and bottom, and a banner shows his name
	// and the name of the phase he starts (the entrance is phase 1). When he roars the screen flashes in the phase's
	// colour. Then the camera goes back to the player.
	// The game can't really pause in multiplayer, so the fight does instead: every player near him is held still (no
	// moving, no items) and can't be hurt, and his attacks still in the air vanish when it starts (BoneRonin.ClearAttacks).
	// It all follows the boss's synced state (AI state and timer), so every client sees the same cinematic at once.
	public class RoninCinematic : ModSystem
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		private const float Range = 6000f;
		// Frames to glide in and out.
		private const int EaseIn = 30, EaseOut = 22;

		private static uint foundAt = uint.MaxValue;
		private static BoneRonin found;

		// The Bone Ronin in a cinematic right now, if any (looked for once per tick).
		public static BoneRonin Active {
			get {
				if (foundAt != Main.GameUpdateCount) {
					foundAt = Main.GameUpdateCount;
					found = null;
					int type = ModContent.NPCType<BoneRonin>();
					for (int i = 0; i < Main.maxNPCs; i++) {
						NPC npc = Main.npc[i];
						if (npc.active && npc.type == type && npc.ModNPC is BoneRonin ronin && ronin.InCinematic) {
							found = ronin;
							break;
						}
					}
				}
				return found;
			}
		}

		// The player is in the cinematic: alive and near him.
		public static bool Holds(Player player) => Active is BoneRonin ronin && player.active && !player.dead && player.Distance(ronin.NPC.Center) < Range;

		// 0 to 1: how far into the cinematic the camera and bars are (eased in and out), for the local player.
		private static float Strength(out BoneRonin ronin)
		{
			ronin = Active;
			if (ronin == null || Main.dedServ || Main.gameMenu || !Holds(Main.LocalPlayer))
				return 0f;
			float t = ronin.CinematicTimer;
			float s = Math.Min(1f, Math.Min(t / EaseIn, (BoneRonin.CinematicLength - t) / EaseOut));
			s = MathHelper.Clamp(s, 0f, 1f);
			return s * s * (3f - 2f * s);
		}

		// Called by ScreenShake.ModifyScreenPosition before it shakes, so the shake still applies on top.
		public static void MoveCamera()
		{
			float s = Strength(out BoneRonin ronin);
			if (s <= 0f)
				return;
			Vector2 goal = ronin.NPC.Center - new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
			Main.screenPosition = Vector2.Lerp(Main.screenPosition, goal, s);
		}

		public override void ModifyTransformMatrix(ref SpriteViewMatrix Transform)
		{
			float s = Strength(out _);
			if (s > 0f)
				Transform.Zoom *= 1f + 0.25f * s;
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Interface Logic 1");
			layers.Insert(index < 0 ? 0 : index + 1, new LegacyGameInterfaceLayer("TerraIndex: Bone Ronin Cinematic", () => {
				Draw(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.None));
		}

		// The phase's colour: his blue (or the ember look's orange), devil red for the last.
		private static Color PhaseColor(int phase) => phase >= 2 ? new Color(255, 45, 35) : RoninLook.Main;

		// Fades in from `inStart` to `inEnd`, out from `outStart` to `outEnd` (in ticks).
		private static float Fade(float t, float inStart, float inEnd, float outStart, float outEnd) =>
			MathHelper.Clamp(Math.Min((t - inStart) / (inEnd - inStart), (outEnd - t) / (outEnd - outStart)), 0f, 1f);

		private static void Draw(SpriteBatch spriteBatch)
		{
			float s = Strength(out BoneRonin ronin);
			if (s <= 0f)
				return;
			float t = ronin.CinematicTimer;
			int phase = ronin.CinematicPhase;
			Color accent = PhaseColor(phase);
			int width = Main.screenWidth, height = Main.screenHeight;
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			// The flash when he roars (the moment the phase changes).
			float flash = 1f - Math.Abs(t - 130f) / 14f;
			if (flash > 0f)
				spriteBatch.Draw(pixel, new Rectangle(0, 0, width, height), new Color(accent.R, accent.G, accent.B, 0) * flash * 0.45f);

			// Black bars.
			int bar = (int)(height * 0.12f * s);
			spriteBatch.Draw(pixel, new Rectangle(0, 0, width, bar), Color.Black);
			spriteBatch.Draw(pixel, new Rectangle(0, height - bar, width, bar), Color.Black);

			// The banner: his name, a line that opens out under it, and the phase's name.
			float title = Fade(t, 20f, 42f, 150f, 172f);
			float subtitle = Fade(t, 45f, 65f, 150f, 172f);
			float line = Fade(t, 30f, 60f, 150f, 172f);
			Vector2 center = new(width / 2f, height * 0.3f);
			if (line > 0f) {
				float half = 260f * (float)Math.Sqrt(line);
				Color glow = new Color(accent.R, accent.G, accent.B, 0);
				BoneRonin.DrawLine(spriteBatch, center + new Vector2(-half, 34f), center + new Vector2(half, 34f), glow * line * 0.35f, 6f);
				BoneRonin.DrawLine(spriteBatch, center + new Vector2(-half, 34f), center + new Vector2(half, 34f), glow * line, 2f);
			}
			if (title > 0f) {
				string name = ronin.NPC.TypeName;
				float scale = 1.25f - 0.2f * title;
				Utils.DrawBorderStringBig(spriteBatch, name, center, Color.Lerp(accent, Color.White, 0.6f) * title, scale, 0.5f, 0.5f);
			}
			if (subtitle > 0f) {
				string text = Language.GetTextValue("Mods.TerraIndex.Messages.RoninPhase" + phase);
				Vector2 at = center + new Vector2(0, 62f + 8f * (1f - subtitle));
				Utils.DrawBorderString(spriteBatch, text, at, accent * subtitle, 1.15f, 0.5f, 0.5f);
			}
		}
	}

	// Holds the players in a cinematic still (they hang where they are, can't use anything) and keeps them from harm.
	public class RoninCinematicPlayer : ModPlayer
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		private bool Held => RoninCinematic.Holds(Player);

		public override void SetControls()
		{
			if (!Held)
				return;
			Player.controlLeft = Player.controlRight = Player.controlUp = Player.controlDown = false;
			Player.controlJump = Player.controlUseItem = Player.controlUseTile = Player.controlThrow = false;
			Player.controlHook = Player.controlMount = Player.controlQuickHeal = Player.controlQuickMana = false;
			Player.controlSmart = Player.controlTorch = false;
		}

		public override void PreUpdateMovement()
		{
			if (!Held)
				return;
			Player.velocity = Vector2.Zero;
			// No fall damage from where they were held.
			Player.fallStart = (int)(Player.position.Y / 16f);
		}

		public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) => Held;

		public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) => !Held;

		public override bool CanBeHitByProjectile(Projectile proj) => !Held;
	}
}
