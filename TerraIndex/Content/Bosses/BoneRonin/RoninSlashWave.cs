using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The Bone Ronin's flying slash: a pale crescent that speeds up a little as it goes.
	public class RoninSlashWave : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 6;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 40;
			Projectile.height = 40;
			Projectile.scale = 1.3f;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 200;
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation();
			if (Projectile.velocity.Length() < 20f)
				Projectile.velocity *= 1.02f;
			if (Projectile.timeLeft < 20)
				Projectile.alpha = (int)(255 * (1 - Projectile.timeLeft / 20f));
			Lighting.AddLight(Projectile.Center, RoninLook.LightVector * 1.2f);
			if (Main.rand.NextBool(4))
				Dust.NewDustPerfect(Projectile.Center, RoninLook.Dust, Projectile.velocity * 0.2f, 0, default, 1.1f).noGravity = true;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			DrawCrescent(Projectile, RoninLook.Additive(RoninLook.Bright));
			return false;
		}

		// Shared with the player's katana slash: the crescent and a fading trail behind it, drawn glowing.
		public static void DrawCrescent(Projectile projectile, Color color)
		{
			Texture2D texture = TextureAssets.Projectile[projectile.type].Value;
			Vector2 origin = texture.Size() / 2f;
			Vector2 center = projectile.Center - Main.screenPosition;
			// A soft glow behind it.
			Texture2D bloom = BoneRonin.Bloom;
			Main.EntitySpriteDraw(bloom, center, null, color * 0.5f * projectile.Opacity, projectile.rotation, bloom.Size() / 2f, new Vector2(0.9f, 1.4f) * projectile.scale, SpriteEffects.None, 0);
			for (int i = projectile.oldPos.Length - 1; i > 0; i--) {
				float fade = (projectile.oldPos.Length - i) / (float)projectile.oldPos.Length;
				Vector2 old = projectile.oldPos[i] + projectile.Size / 2f - Main.screenPosition;
				Main.EntitySpriteDraw(texture, old, null, color * fade * 0.4f * projectile.Opacity, projectile.oldRot[i], origin, projectile.scale * 0.8f, SpriteEffects.None, 0);
			}
			Main.EntitySpriteDraw(texture, center, null, color * projectile.Opacity, projectile.rotation, origin, projectile.scale * 0.8f, SpriteEffects.None, 0);
			// A brighter, thinner copy on top for a white-hot edge.
			Main.EntitySpriteDraw(texture, center, null, new Color(255, 255, 255, 0) * 0.6f * projectile.Opacity, projectile.rotation, origin, projectile.scale * new Vector2(0.6f, 0.75f), SpriteEffects.None, 0);
		}
	}
}
