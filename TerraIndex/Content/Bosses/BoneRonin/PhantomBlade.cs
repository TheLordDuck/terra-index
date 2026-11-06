using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// Blade rain: a ghostly katana that appears pointing down, shows a faint line where it will fall, then drops.
	// It can't hurt anyone while it is still appearing.
	public class PhantomBlade : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		private const int Appear = 40;

		private ref float Timer => ref Projectile.ai[0];

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
		}

		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 50;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 200;
			Projectile.alpha = 255;
		}

		public override void AI()
		{
			Timer++;
			if (Timer < Appear) {
				Projectile.velocity = Vector2.Zero;
				Projectile.alpha = (int)(255 * (1 - Timer / Appear) + 40 * (Timer / Appear));
			}
			else if (Timer == Appear) {
				Projectile.velocity = new Vector2(0, 4f);
				SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.4f }, Projectile.Center);
			}
			else
				Projectile.velocity.Y = System.Math.Min(Projectile.velocity.Y + 1.2f, 26f);
			RoninVoid.Light(Projectile.Center, 0.8f * Projectile.Opacity, Tint);
			if (Timer > Appear && Main.rand.NextBool(3))
				RoninVoid.Spark(Projectile.Center + Main.rand.NextVector2Circular(6f, 22f), new Vector2(0, -1f), 1f, Tint);
		}

		// ai[2]: from an echo (see BoneRonin.EchoTint): its colour, and it only hurts that echo's player.
		private Color? Tint => BoneRonin.EchoTint(Projectile);

		public override bool CanHitPlayer(Player target) => Timer >= Appear && BoneRonin.EchoCanHit(Projectile, target);

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 center = Projectile.Center - Main.screenPosition;
			if (Timer < Appear) {
				// Where it will fall: a faint white band that sharpens as it is about to drop.
				Vector2 top = Projectile.Bottom - Main.screenPosition;
				RoninVoid.DrawWarning(top, top + new Vector2(0, 900), 12f, Timer / Appear, Tint);
			}
			// Falling: a stretched white glow behind it, longer the faster it goes.
			Texture2D bloom = BoneRonin.Bloom;
			float stretch = 1f + Projectile.velocity.Y / 12f;
			Main.EntitySpriteDraw(bloom, center - new Vector2(0, 10 * (stretch - 1)), null, RoninVoid.AuraOf(Tint) * 0.45f * Projectile.Opacity, 0f, bloom.Size() / 2f, new Vector2(0.5f, 1.2f * stretch), SpriteEffects.None, 0);
			Texture2D texture = TextureAssets.Projectile[Type].Value;
			RoninVoid.DrawSprite(texture, center, null, 0f, texture.Size() / 2f, Vector2.One, SpriteEffects.None, Projectile.Opacity, 2f, 0f, Tint);
			return false;
		}
	}
}
