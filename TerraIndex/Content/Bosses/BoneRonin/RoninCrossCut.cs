using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// One of the Bone Ronin's cross cuts: an X of two long cuts, turned at random (ai[0]), centred on where the player
	// was when it appeared (ai[2]: from an echo, see BoneRonin.EchoTint). First it only shows where it will cut (a white X that brightens and flickers) and can't
	// hurt; after ai[1] ticks the two cuts slash across it, one just after the other, and hurt for a moment, then fade.
	public class RoninCrossCut : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override string Texture => "TerraIndex/Content/Bosses/BoneRonin/IaidoCut";

		// Half the length of each cut, its width (for hits), the gap between the two cuts, how long each takes to slash
		// across, how long they hurt and how long they take to fade.
		public const float Reach = 340f;
		private const float Width = 38f;
		private const int SecondCut = 3, SlashTicks = 4, HurtTicks = 10, FadeTicks = 26;

		private float Rotation => Projectile.ai[0];
		private int Warning => Math.Max(1, (int)Projectile.ai[1]);
		private ref float Timer => ref Projectile.localAI[0];

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
		}

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 600;
		}

		// The two cuts, end to end; the second crosses the first at a right angle.
		private (Vector2 from, Vector2 to) Cut(int i)
		{
			Vector2 along = (Rotation + i * MathHelper.PiOver2).ToRotationVector2() * Reach;
			// The second cut goes the other way round, like a blade coming back across.
			return i == 0 ? (Projectile.Center - along, Projectile.Center + along) : (Projectile.Center + along, Projectile.Center - along);
		}

		private int CutStart(int i) => Warning + i * SecondCut;

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			Timer++;
			if (Timer == 1)
				SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.3f }, Projectile.Center);
			for (int i = 0; i < 2; i++) {
				if (Timer != CutStart(i))
					continue;
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f - i * 0.3f }, Projectile.Center);
				if (i == 0) {
					SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
					ScreenShake.Shake(Projectile.Center, 5f);
				}
				var (from, to) = Cut(i);
				for (float t = 0f; t <= 1f; t += 0.04f)
					RoninVoid.Spark(Vector2.Lerp(from, to, t), Main.rand.NextVector2Circular(3f, 3f), 1.4f, Tint);
			}
			// Lights up where it will cut, and the cut itself.
			float light = Timer < Warning ? 0.3f + 0.4f * Timer / Warning : 1.4f * FadeLeft;
			for (int i = 0; i < 2; i++) {
				var (from, to) = Cut(i);
				for (float t = 0f; t <= 1f; t += 0.25f)
					RoninVoid.Light(Vector2.Lerp(from, to, t), light, Tint);
			}
			if (Timer >= Warning + SecondCut + FadeTicks)
				Projectile.Kill();
		}

		private float FadeLeft => MathHelper.Clamp(1f - (Timer - Warning) / (SecondCut + FadeTicks), 0f, 1f);

		// ai[2]: from an echo (see BoneRonin.EchoTint): its colour, and it only hurts that echo's player.
		private Color? Tint => BoneRonin.EchoTint(Projectile);

		public override bool CanHitPlayer(Player target) => BoneRonin.EchoCanHit(Projectile, target);

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			for (int i = 0; i < 2; i++) {
				if (Timer < CutStart(i) || Timer >= CutStart(i) + HurtTicks)
					continue;
				var (from, to) = Cut(i);
				float point = 0f;
				if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, to, Width, ref point))
					return true;
			}
			return false;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 screen = Main.screenPosition;
			if (Timer < Warning) {
				// The warning: both lines of the X, and a small mark where they cross.
				float progress = Timer / Warning;
				for (int i = 0; i < 2; i++) {
					var (from, to) = Cut(i);
					RoninVoid.DrawWarning(from - screen, to - screen, Width, progress, Tint);
				}
				Texture2D bloom = BoneRonin.Bloom;
				Main.EntitySpriteDraw(bloom, Projectile.Center - screen, null, RoninVoid.AuraOf(Tint) * (0.2f + 0.4f * progress), 0f, bloom.Size() / 2f, 0.3f + 0.3f * progress, SpriteEffects.None, 0);
				return false;
			}
			// The cuts: each slashes across from one end to the other, then thins out and fades.
			for (int i = 0; i < 2; i++) {
				if (Timer < CutStart(i))
					continue;
				var (from, to) = Cut(i);
				float drawn = MathHelper.Clamp((Timer - CutStart(i) + 1) / SlashTicks, 0f, 1f);
				float fade = MathHelper.Clamp(1f - (Timer - CutStart(i)) / FadeTicks, 0f, 1f);
				RoninVoid.DrawLine(from - screen, Vector2.Lerp(from, to, drawn) - screen, 4f + 22f * fade, fade, Tint);
			}
			// A white flash where the cuts cross.
			float flash = MathHelper.Clamp(1f - (Timer - Warning) / 12f, 0f, 1f);
			if (flash > 0f) {
				Texture2D bloom = BoneRonin.Bloom;
				Main.EntitySpriteDraw(bloom, Projectile.Center - screen, null, RoninVoid.AuraOf(Tint) * flash, 0f, bloom.Size() / 2f, 1.5f * flash + 0.3f, SpriteEffects.None, 0);
			}
			return false;
		}
	}
}
