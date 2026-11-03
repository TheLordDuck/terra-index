using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The Bone Ronin's katana: a fast sword that throws short-lived slash waves.
	public class RoninKatana : ModItem
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override void SetDefaults()
		{
			Item.width = 84;
			Item.height = 84;
			Item.damage = 230;
			Item.DamageType = DamageClass.Melee;
			Item.useTime = 16;
			Item.useAnimation = 16;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.knockBack = 6f;
			Item.value = Item.sellPrice(gold: 20);
			Item.rare = ItemRarityID.Red;
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = true;
			Item.shoot = ModContent.ProjectileType<KatanaSlash>();
			Item.shootSpeed = 16f;
		}
	}

	// The katana's slash wave: the boss's crescent, golden, going through 3 enemies and fading after a short flight.
	public class KatanaSlash : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override string Texture => "TerraIndex/Content/Bosses/BoneRonin/RoninSlashWave";

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 6;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = 3;
			Projectile.timeLeft = 40;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation();
			if (Projectile.timeLeft < 12)
				Projectile.alpha = (int)(255 * (1 - Projectile.timeLeft / 12f));
			Lighting.AddLight(Projectile.Center, 0.8f, 0.65f, 0.2f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			RoninSlashWave.DrawCrescent(Projectile, new Color(255, 220, 140, 0));
			return false;
		}
	}
}
