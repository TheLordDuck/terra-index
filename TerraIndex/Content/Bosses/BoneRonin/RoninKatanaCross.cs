using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The katana's right click, while it is still pressed: the hand goes to the hilt and it waits to see what you do.
	// Let go quickly and it is a cross cut where the cursor is (RoninKatanaCross); hold it and it becomes the iaido
	// (RoninKatanaIaido) towards the cursor, if that is ready. Holding it with the iaido on cooldown waits for you to let
	// go, then does the cross cut. Only the owner reads the mouse; what it spawns is synced like any projectile.
	public class RoninKatanaHold : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override string Texture => "TerraIndex/Content/Bosses/BoneRonin/RoninKatana";

		// How long the button must be held for the iaido (a quarter of a second).
		public const int HoldTime = 15;
		// The cross cut lands where you click, up to 45 tiles away.
		private const float CrossRange = 720f;

		private ref float Timer => ref Projectile.localAI[0];

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = false;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 3600;
		}

		public override bool? CanDamage() => false;

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];
			if (!player.active || player.dead || player.CCed) {
				Projectile.Kill();
				return;
			}
			Timer++;
			Projectile.Center = player.MountedCenter;
			Projectile.timeLeft = 2;
			player.heldProj = Projectile.whoAmI;
			player.itemTime = player.itemAnimation = 2;
			// Hand on the hilt at the hip.
			player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, player.direction * 0.6f);
			if (Projectile.owner != Main.myPlayer)
				return;
			player.ChangeDir(Main.MouseWorld.X >= player.Center.X ? 1 : -1);

			var modPlayer = player.GetModPlayer<RoninKatanaPlayer>();
			bool held = Main.mouseRight;
			Vector2 toMouse = Main.MouseWorld - player.MountedCenter;
			if (held && Timer >= HoldTime && modPlayer.iaidoCooldown <= 0) {
				// Iaido: up to 30 tiles towards the cursor.
				Vector2 dash = toMouse.Length() > RoninKatanaIaido.MaxDash ? toMouse.SafeNormalize(Vector2.UnitX) * RoninKatanaIaido.MaxDash : toMouse;
				if (dash.Length() < 80f)
					dash = toMouse.SafeNormalize(Vector2.UnitX * player.direction) * 80f;
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), player.MountedCenter, Vector2.Zero, ModContent.ProjectileType<RoninKatanaIaido>(), (int)(Projectile.damage * 3.5f), Projectile.knockBack, player.whoAmI, 0f, dash.X, dash.Y);
				modPlayer.iaidoCooldown = RoninKatanaPlayer.IaidoCooldown;
				Projectile.Kill();
				return;
			}
			if (held)
				return;
			if (modPlayer.crossCooldown <= 0) {
				// Three cross cuts, one after another, on the nearest enemies (round again if there are fewer than
				// three); with no enemy in reach, all three where you clicked.
				List<NPC> targets = NearestEnemies(player.MountedCenter, Cuts);
				Vector2 cursor = player.MountedCenter + (toMouse.Length() > CrossRange ? toMouse.SafeNormalize(Vector2.UnitX) * CrossRange : toMouse);
				for (int i = 0; i < Cuts; i++) {
					NPC target = targets.Count > 0 ? targets[i % targets.Count] : null;
					Vector2 spot = target?.Center ?? cursor;
					float rotation = Main.rand.NextFloat(MathHelper.PiOver2);
					Projectile.NewProjectile(Projectile.GetSource_FromThis(), spot, Vector2.Zero, ModContent.ProjectileType<RoninKatanaCross>(), (int)(Projectile.damage * 1.5f), Projectile.knockBack, player.whoAmI, rotation, target?.whoAmI ?? -1, i * CutGap);
				}
				modPlayer.crossCooldown = RoninKatanaPlayer.CrossCooldown;
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f, Volume = 0.6f }, player.Center);
			}
			Projectile.Kill();
		}

		// Cross cuts per click, and the ticks between them.
		private const int Cuts = 3, CutGap = 5;

		// Up to `count` enemies that can be attacked, nearest first, within reach and in sight.
		private static List<NPC> NearestEnemies(Vector2 from, int count)
		{
			var found = new List<NPC>();
			foreach (NPC npc in Main.ActiveNPCs)
				if (npc.CanBeChasedBy() && npc.Distance(from) < CrossRange && Collision.CanHitLine(from, 1, 1, npc.position, npc.width, npc.height))
					found.Add(npc);
			found.Sort((a, b) => a.DistanceSQ(from).CompareTo(b.DistanceSQ(from)));
			if (found.Count > count)
				found.RemoveRange(count, found.Count - count);
			return found;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// A glint at the hilt that grows while the button is held (the iaido is coming).
			Player player = Main.player[Projectile.owner];
			float charge = MathHelper.Clamp(Timer / HoldTime, 0f, 1f);
			if (player.GetModPlayer<RoninKatanaPlayer>().iaidoCooldown > 0)
				charge *= 0.3f;
			Texture2D bloom = BoneRonin.Bloom;
			Vector2 hip = player.MountedCenter + new Vector2(player.direction * 8f, 6f) - Main.screenPosition;
			Color glow = new Color(255, 240, 200, 0) * charge;
			Main.EntitySpriteDraw(bloom, hip, null, glow, 0f, bloom.Size() / 2f, new Vector2(1.2f, 0.1f) * charge, SpriteEffects.None, 0);
			Main.EntitySpriteDraw(bloom, hip, null, glow, MathHelper.PiOver2, bloom.Size() / 2f, new Vector2(1.2f, 0.1f) * charge, SpriteEffects.None, 0);
			return false;
		}
	}

	// The katana's cross cut, the boss's in gold: an X turned at random (ai[0]). Each click makes three, one after
	// another (ai[2]: how long this one waits), each on one of the nearest enemies (ai[1], -1 for none: where you
	// clicked), and it follows that enemy until it cuts. It flashes in for a moment, then two cuts slash across it,
	// one just after the other, and fade.
	public class RoninKatanaCross : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override string Texture => "TerraIndex/Content/Bosses/BoneRonin/IaidoCut";

		private const float Reach = 170f, Width = 40f;
		private const int WarnTime = 8, SecondCut = 3, SlashTicks = 4, HurtTicks = 6, FadeTicks = 22;

		private float Rotation => Projectile.ai[0];
		private int Delay => (int)Projectile.ai[2];
		private int Warning => Delay + WarnTime;
		private ref float Timer => ref Projectile.localAI[0];

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
		}

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 600;
			// Each of the two cuts hits.
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = SecondCut + HurtTicks - 2;
		}

		private (Vector2 from, Vector2 to) Cut(int i)
		{
			Vector2 along = (Rotation + i * MathHelper.PiOver2).ToRotationVector2() * Reach;
			return i == 0 ? (Projectile.Center - along, Projectile.Center + along) : (Projectile.Center + along, Projectile.Center - along);
		}

		private int CutStart(int i) => Warning + i * SecondCut;

		public override void AI()
		{
			Projectile.velocity = Vector2.Zero;
			Timer++;
			// Follows its enemy until the first cut.
			int index = (int)Projectile.ai[1];
			if (Timer < Warning && index >= 0 && index < Main.maxNPCs && Main.npc[index].active && Main.npc[index].CanBeChasedBy())
				Projectile.Center = Main.npc[index].Center;
			if (Timer >= Warning + SecondCut + FadeTicks) {
				Projectile.Kill();
				return;
			}
			for (int i = 0; i < 2; i++) {
				if (Timer != CutStart(i))
					continue;
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.4f - i * 0.3f }, Projectile.Center);
				if (i == 0) {
					SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.5f, Pitch = 0.5f }, Projectile.Center);
					ScreenShake.Shake(Projectile.Center, 3f);
				}
				if (!Main.dedServ) {
					var (from, to) = Cut(i);
					for (float t = 0f; t <= 1f; t += 0.05f)
						Dust.NewDustPerfect(Vector2.Lerp(from, to, t), Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.3f).noGravity = true;
				}
			}
			Lighting.AddLight(Projectile.Center, 0.9f, 0.75f, 0.4f);
		}

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

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 8; i++)
				Dust.NewDustPerfect(target.Center, DustID.GoldFlame, Main.rand.NextVector2Circular(6f, 6f), 0, default, 1.4f).noGravity = true;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 screen = Main.screenPosition;
			if (Timer < Delay)
				return false;
			if (Timer < Warning) {
				// A thin golden X that sharpens.
				float progress = (Timer - Delay) / WarnTime;
				for (int i = 0; i < 2; i++) {
					var (from, to) = Cut(i);
					BoneRonin.DrawLine(Main.spriteBatch, from - screen, to - screen, new Color(255, 225, 150, 0) * (0.2f + 0.4f * progress), 4f - 2.5f * progress);
				}
				return false;
			}
			for (int i = 0; i < 2; i++) {
				if (Timer < CutStart(i))
					continue;
				var (from, to) = Cut(i);
				float drawn = MathHelper.Clamp((Timer - CutStart(i) + 1) / SlashTicks, 0f, 1f);
				float fade = MathHelper.Clamp(1f - (Timer - CutStart(i)) / FadeTicks, 0f, 1f);
				Vector2 a = from - screen, b = Vector2.Lerp(from, to, drawn) - screen;
				BoneRonin.DrawLine(Main.spriteBatch, a, b, new Color(120, 200, 255, 0) * fade, 40f * fade);
				BoneRonin.DrawLine(Main.spriteBatch, a, b, new Color(255, 230, 160, 0) * fade, 16f * fade);
				BoneRonin.DrawLine(Main.spriteBatch, a, b, new Color(255, 255, 255, 0) * fade, 4f * fade);
			}
			float flash = MathHelper.Clamp(1f - (Timer - Warning) / 10f, 0f, 1f);
			if (flash > 0f) {
				Texture2D bloom = BoneRonin.Bloom;
				Main.EntitySpriteDraw(bloom, Projectile.Center - screen, null, new Color(255, 235, 180, 0) * flash, 0f, bloom.Size() / 2f, 1.1f * flash + 0.2f, SpriteEffects.None, 0);
			}
			return false;
		}
	}
}
