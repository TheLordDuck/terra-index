using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The Iaido cut: a straight line from where the Bone Ronin stood (the projectile's position) to where he lands
	// (ai[0], ai[1]). It only hurts for its first few ticks, then fades.
	public class IaidoCut : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		private const int Life = 20;
		private const int HurtTicks = 8;
		private const float Width = 56f;

		private Vector2 End => new(Projectile.ai[0], Projectile.ai[1]);

		public override void SetStaticDefaults()
		{
			// Long line: draw it even when its start is off screen.
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
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
			Projectile.timeLeft = Life;
		}

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			if (Projectile.localAI[0] == 0f) {
				Projectile.localAI[0] = 1f;
				if (!Main.dedServ) {
					for (float t = 0f; t <= 1f; t += 0.02f) {
						Vector2 spot = Vector2.Lerp(Projectile.Center, End, t);
						RoninVoid.Spark(spot, Main.rand.NextVector2Circular(3f, 3f), 1.5f, Tint);
					}
				}
			}
			RoninVoid.Light(Vector2.Lerp(Projectile.Center, End, 0.5f), 1.5f * Projectile.timeLeft / (float)Life, Tint);
		}

		// ai[2]: from an echo (see BoneRonin.EchoTint): its colour, and it only hurts that echo's player.
		private Color? Tint => BoneRonin.EchoTint(Projectile);

		public override bool CanHitPlayer(Player target) => BoneRonin.EchoCanHit(Projectile, target);

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (Projectile.timeLeft <= Life - HurtTicks)
				return false;
			float point = 0f;
			return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, End, Width, ref point);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			float left = Projectile.timeLeft / (float)Life;
			Vector2 start = Projectile.Center - Main.screenPosition;
			Vector2 end = End - Main.screenPosition;
			RoninVoid.DrawLine(start, end, 6f + 20f * left, left, Tint);
			return false;
		}
	}
}
