using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// How the Bone Ronin's attacks look: dark energy wrapped in a white aura. The core is drawn as an ordinary (opaque)
	// colour, so it stays dark over a bright sky; the aura is drawn as light, so it shines in the dark and lights up the
	// area around it. Shared by all his attacks (slash waves, phantom blades, the iaido and cross cuts, the arc of his
	// swings).
	// An echo's attacks pass `tint`, the echo's colour (see BoneRonin.EchoColor): then they glow in that colour
	// instead, see-through like the echo itself, so each player can tell which attacks come from their echo.
	public static class RoninVoid
	{
		public static Color Aura => new(255, 255, 255, 0);

		// The aura (drawn as light): white, or the echo's colour.
		public static Color AuraOf(Color? tint) => tint is Color c ? new Color(c.R, c.G, c.B, 0) : Aura;

		// The core, faded by `opacity`: dark (drawn normally, not as light), or for an echo its colour, pale and
		// half see-through.
		public static Color Core(float opacity, Color? tint = null)
		{
			if (tint is Color c) {
				Color pale = Color.Lerp(c, Color.White, 0.25f);
				return new Color(pale.R, pale.G, pale.B, 150) * opacity;
			}
			return RoninLook.Void * opacity;
		}

		// Light around the attack, so it can be seen at night and underground.
		public static void Light(Vector2 position, float strength = 1f, Color? tint = null)
		{
			Vector3 color = tint is Color c ? c.ToVector3() : new Vector3(0.85f, 0.85f, 0.95f);
			Lighting.AddLight(position, color * strength);
		}

		// A puff of the attack's particles: dark smoke and white sparks, or glowing sparks in the echo's colour.
		public static void Spark(Vector2 position, Vector2 velocity, float scale = 1.2f, Color? tint = null)
		{
			if (Main.dedServ)
				return;
			Dust dust;
			if (tint is Color c)
				dust = Dust.NewDustPerfect(position, DustID.TintableDustLighted, velocity, 0, c, scale);
			else {
				bool white = Main.rand.NextBool(2);
				dust = Dust.NewDustPerfect(position, white ? DustID.WhiteTorch : RoninLook.VoidDust, velocity, white ? 0 : 60, default, white ? scale : scale * 1.3f);
			}
			dust.noGravity = true;
		}

		// A sprite in the attack's look: a soft glow, a rim (the sprite drawn a few pixels out in eight directions) and
		// the core on top.
		public static void DrawSprite(Texture2D texture, Vector2 position, Rectangle? source, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float opacity, float rim = 2f, float glow = 0.5f, Color? tint = null)
		{
			Color aura = AuraOf(tint);
			if (glow > 0f) {
				Texture2D bloom = BoneRonin.Bloom;
				float size = (source?.Width ?? texture.Width) * scale.X / bloom.Width * 2.2f;
				Main.EntitySpriteDraw(bloom, position, null, aura * glow * opacity, rotation, bloom.Size() / 2f, size, SpriteEffects.None, 0);
			}
			for (int i = 0; i < 8; i++) {
				Vector2 offset = (MathHelper.PiOver4 * i).ToRotationVector2() * rim;
				Main.EntitySpriteDraw(texture, position + offset, source, aura * 0.55f * opacity, rotation, origin, scale, effects, 0);
			}
			Main.EntitySpriteDraw(texture, position, source, Core(opacity, tint), rotation, origin, scale, effects, 0);
		}

		// A straight cut in the attack's look, `width` pixels wide: a wide faint glow, a bright edge and the core, with
		// a thin bright seam down the middle.
		public static void DrawLine(Vector2 start, Vector2 end, float width, float opacity, Color? tint = null)
		{
			SpriteBatch spriteBatch = Main.spriteBatch;
			Color aura = AuraOf(tint);
			BoneRonin.DrawLine(spriteBatch, start, end, aura * 0.25f * opacity, width * 2.4f);
			BoneRonin.DrawLine(spriteBatch, start, end, aura * 0.9f * opacity, width + 6f);
			BoneRonin.DrawLine(spriteBatch, start, end, Core(opacity, tint), width);
			BoneRonin.DrawLine(spriteBatch, start, end, Aura * 0.35f * opacity, System.Math.Max(1f, width * 0.12f));
		}

		// Where an attack will land: a faint band that narrows, brightens and starts to flicker as the hit nears
		// (`progress` 0 to 1), around a thin line of the core.
		public static void DrawWarning(Vector2 start, Vector2 end, float width, float progress, Color? tint = null)
		{
			SpriteBatch spriteBatch = Main.spriteBatch;
			Color aura = AuraOf(tint);
			float flicker = progress > 0.6f ? 0.75f + 0.25f * (float)System.Math.Sin(Main.GameUpdateCount * 1.3f) : 1f;
			BoneRonin.DrawLine(spriteBatch, start, end, aura * (0.08f + 0.22f * progress) * flicker, width * (1.6f - 0.6f * progress));
			BoneRonin.DrawLine(spriteBatch, start, end, aura * (0.35f + 0.65f * progress) * flicker, 2f + 3f * progress);
			BoneRonin.DrawLine(spriteBatch, start, end, Core(0.5f + 0.5f * progress, tint), 1f + 1.5f * progress);
		}
	}
}
