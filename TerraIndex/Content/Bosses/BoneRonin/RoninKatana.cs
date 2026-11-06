using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The Bone Ronin's katana, the last weapon of the game (he comes after Supreme Calamitas). Its swings are drawn by
	// code, like the boss's own sword arm:
	// - Left click: a three-strike combo. A rising cut and a falling cut, each throwing a golden crescent, then a
	//   finisher: the blade is drawn back and spun all the way round the player, and a great crescent flies out.
	//   Each swing eases in (a short pull back), cuts fast and slows at the end, leaving a smear of light.
	// - Right click, held: iaido, like the boss's. A stance with a glint and a line to where you will land, a dash through
	//   everything on that line (you can't be hurt while dashing), and a moment later, as the blade clicks back into
	//   the scabbard, the cut lands on everything along the line. 3 second cooldown, with a chime when it is ready.
	// - Right click, a quick click: a cross cut, like the boss's, where you clicked (RoninKatanaCross). Half a second
	//   cooldown.
	// - Every 8 hits, phantom katanas appear above the enemy hit and fall on it, like the boss's blade rain.
	public class RoninKatana : ModItem
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override void SetDefaults()
		{
			Item.width = 84;
			Item.height = 84;
			Item.damage = 1400;
			Item.crit = 15;
			Item.DamageType = DamageClass.Melee;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.knockBack = 7f;
			Item.value = Item.sellPrice(platinum: 1, gold: 20);
			Item.rare = ModContent.TryFind("CalamityMod/Violet", out ModRarity violet) ? violet.Type : ItemRarityID.Purple;
			Item.autoReuse = true;
			Item.shoot = ModContent.ProjectileType<RoninKatanaSwing>();
			Item.shootSpeed = 1f;
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool CanUseItem(Player player)
		{
			foreach (int busy in new[] { ModContent.ProjectileType<RoninKatanaSwing>(), ModContent.ProjectileType<RoninKatanaIaido>(), ModContent.ProjectileType<RoninKatanaHold>() })
				if (player.ownedProjectileCounts[busy] > 0)
					return false;
			var modPlayer = player.GetModPlayer<RoninKatanaPlayer>();
			return player.altFunctionUse != 2 || modPlayer.crossCooldown <= 0 || modPlayer.iaidoCooldown <= 0;
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			var modPlayer = player.GetModPlayer<RoninKatanaPlayer>();
			Vector2 toMouse = Main.MouseWorld - player.MountedCenter;
			if (player.altFunctionUse == 2) {
				// Right click: a quick click is a cross cut where you clicked, holding it is the iaido (RoninKatanaHold
				// tells which).
				Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, ModContent.ProjectileType<RoninKatanaHold>(), damage, knockback, player.whoAmI);
				return false;
			}
			// The combo goes on if the next swing comes soon enough.
			if (Main.GameUpdateCount - modPlayer.lastSwing > 45)
				modPlayer.combo = 0;
			int step = modPlayer.combo;
			modPlayer.combo = (modPlayer.combo + 1) % 3;
			modPlayer.lastSwing = Main.GameUpdateCount;
			float speed = player.GetTotalAttackSpeed(DamageClass.Melee);
			float duration = (step == 2 ? 34f : 20f) / Math.Max(0.5f, speed);
			int swingDamage = step == 2 ? (int)(damage * 1.5f) : damage;
			Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, type, swingDamage, knockback, player.whoAmI, step, toMouse.ToRotation(), duration);
			// The specter behind you repeats the swing a moment later, for half the damage. The last one makes way.
			int specter = ModContent.ProjectileType<RoninKatanaSpecter>();
			foreach (Projectile old in Main.ActiveProjectiles)
				if (old.type == specter && old.owner == player.whoAmI)
					old.Kill();
			Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, specter, swingDamage / 2, knockback, player.whoAmI, step, toMouse.ToRotation(), duration);
			return false;
		}
	}

	// The combo's state, the iaido cooldown and the phantom blade charge.
	public class RoninKatanaPlayer : ModPlayer
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public const int IaidoCooldown = 180, CrossCooldown = 30;
		public int combo;
		public uint lastSwing;
		public int iaidoCooldown, crossCooldown;
		private int rainCharge;

		public override void PostUpdate()
		{
			if (crossCooldown > 0)
				crossCooldown--;
			if (iaidoCooldown > 0 && --iaidoCooldown == 0 && Player.whoAmI == Main.myPlayer) {
				// Ready again: a chime and a glint at the hip.
				SoundEngine.PlaySound(SoundID.MaxMana, Player.Center);
				for (int i = 0; i < 12; i++)
					Dust.NewDustPerfect(Player.Center, DustID.GoldFlame, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.2f).noGravity = true;
			}
		}

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Player.whoAmI != Main.myPlayer)
				return;
			if (proj.type != ModContent.ProjectileType<RoninKatanaSwing>() && proj.type != ModContent.ProjectileType<RoninKatanaSpecter>() && proj.type != ModContent.ProjectileType<KatanaSlash>() && proj.type != ModContent.ProjectileType<RoninKatanaIaido>() && proj.type != ModContent.ProjectileType<RoninKatanaCross>())
				return;
			if (++rainCharge < 8)
				return;
			rainCharge = 0;
			// Phantom katanas appear in an arc above the enemy, one after another, and fall on it.
			int type = ModContent.ProjectileType<RoninKatanaBlade>();
			for (int i = 0; i < 5; i++) {
				Vector2 spot = target.Center + new Vector2(-200f + i * 100f, -300f - Math.Abs(i - 2) * -30f);
				Projectile.NewProjectile(Player.GetSource_OnHit(target), spot, Vector2.Zero, type, (int)(proj.damage * 0.6f), 2f, Player.whoAmI, i * 5, target.whoAmI);
			}
			SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f }, target.Center);
		}
	}

	// One swing of the combo, held in the player's hand. ai[0]: the step (0 rising cut, 1 falling cut, 2 finisher);
	// ai[1]: the aim angle; ai[2]: how long it lasts.
	public class RoninKatanaSwing : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override string Texture => "TerraIndex/Content/Bosses/BoneRonin/RoninKatana";

		// In the item sprite: where the hand holds it, and how far the point is from there.
		private static readonly Vector2 Grip = new(16, 76);
		private const float BladeLength = 100f;

		private int Step => (int)Projectile.ai[0];
		private float Aim => Projectile.ai[1];
		private float Duration => Math.Max(1f, Projectile.ai[2]);
		private ref float Timer => ref Projectile.localAI[0];
		private int Side => Math.Cos(Aim) >= 0 ? 1 : -1;
		private float BladeScale => Step == 2 ? 1.4f : 1.15f;
		// How much of the swing is the pull back before the cut.
		private float Windup => Step == 2 ? 0.3f : 0.2f;

		private readonly List<float> oldAngles = new();
		private bool started, cutStarted, waveThrown;

		// The specter (RoninKatanaSpecter): the same swing by a see-through blue Ronin floating behind and above the
		// player, starting a moment later.
		protected virtual bool Specter => false;
		private const int SpecterDelay = 8;
		private static readonly Color SpecterBlue = new(120, 185, 255);
		private Player Owner => Main.player[Projectile.owner];
		// Fades in while it waits, and out at the end.
		private float SpecterOpacity => MathHelper.Clamp((Timer + SpecterDelay) / SpecterDelay, 0f, 1f) * (Progress > 0.8f ? (1f - Progress) / 0.2f : 1f);
		private Vector2 SpecterSpot => Owner.MountedCenter + new Vector2(-Side * 34f, -50f + (float)Math.Sin(Main.GameUpdateCount * 0.08f) * 3f);

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.ownerHitCheck = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.timeLeft = 2;
		}

		// The blade's angle at progress t (0-1): a pull back, then a fast cut that slows at the end. Angles are relative
		// to the aim and mirrored when facing left.
		private float AngleAt(float t)
		{
			(float from, float to) = Step switch {
				0 => (1.7f, -2.0f),
				1 => (-2.0f, 1.7f),
				// Finisher: from high behind, all the way round the player and on past the aim.
				_ => (-2.2f, -2.2f + MathHelper.TwoPi + 0.9f),
			};
			float back = Math.Sign(to - from) * -0.35f;
			float local;
			if (t < Windup)
				local = from + back * (float)Math.Sin(t / Windup * MathHelper.PiOver2);
			else {
				float q = (t - Windup) / (1f - Windup);
				float eased = 1f - (float)Math.Pow(1f - q, 4);
				local = from + back + (to - from - back) * eased;
			}
			return Aim + Side * local;
		}

		private float Progress => Timer / Duration;
		private bool Cutting => Progress >= Windup && Progress < Windup + (1f - Windup) * 0.7f;
		private Vector2 Tip => Projectile.Center + Projectile.rotation.ToRotationVector2() * BladeLength * BladeScale;

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];
			if (!player.active || player.dead) {
				Projectile.Kill();
				return;
			}
			if (!started) {
				started = true;
				Timer = Specter ? -SpecterDelay : 0;
			}
			Timer++;
			if (Progress >= 1f) {
				Projectile.Kill();
				return;
			}
			Projectile.timeLeft = 2;
			if (Specter) {
				Projectile.Center = SpecterSpot;
				if (Timer <= 0) {
					// Still appearing: blue wisps gathering.
					Projectile.rotation = AngleAt(0f);
					if (!Main.dedServ)
						Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(16f, 30f), DustID.DungeonSpirit, Vector2.Zero, 100, default, 0.9f).noGravity = true;
					return;
				}
			}
			else {
				player.ChangeDir(Side);
				player.heldProj = Projectile.whoAmI;
				player.itemTime = player.itemAnimation = 2;
				Projectile.Center = player.RotatedRelativePoint(player.MountedCenter);
			}
			Projectile.rotation = AngleAt(Progress);
			oldAngles.Insert(0, Projectile.rotation);
			if (oldAngles.Count > 8)
				oldAngles.RemoveAt(oldAngles.Count - 1);
			if (!Specter)
				player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);

			if (!cutStarted && Progress >= Windup) {
				cutStarted = true;
				// The specter's cut sounds higher and softer, like an echo.
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = (Step == 2 ? -0.5f : 0.1f * Step) + (Specter ? 0.35f : 0f), Volume = Specter ? 0.55f : 1f }, Projectile.Center);
				if (Step == 2)
					SoundEngine.PlaySound(SoundID.Item60 with { Pitch = Specter ? 0.5f : 0.2f, Volume = Specter ? 0.35f : 0.6f }, Projectile.Center);
			}
			// A crescent flies out mid-cut (the finisher's, big, at the end of its spin).
			float release = Step == 2 ? 0.85f : Windup + (1f - Windup) * 0.3f;
			if (!waveThrown && Progress >= release) {
				waveThrown = true;
				if (Projectile.owner == Main.myPlayer) {
					Vector2 dir = Aim.ToRotationVector2();
					int damage = Step == 2 ? Projectile.damage : Projectile.damage / 2;
					Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + dir * 30f, dir * (Step == 2 ? 22f : 18f), ModContent.ProjectileType<KatanaSlash>(), damage, Projectile.knockBack, Projectile.owner, Step == 2 ? 1f : 0f);
				}
			}
			if (Cutting && !Main.dedServ) {
				int dustType = Specter ? DustID.DungeonSpirit : Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch;
				Dust dust = Dust.NewDustPerfect(Tip, dustType, Main.rand.NextVector2Circular(1.5f, 1.5f), 0, default, 1.2f);
				dust.noGravity = true;
			}
			if (Specter)
				Lighting.AddLight(Projectile.Center, SpecterBlue.ToVector3() * 0.5f * SpecterOpacity);
			else
				Lighting.AddLight(Tip, 0.7f, 0.65f, 0.4f);
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (!Cutting || Timer <= 0)
				return false;
			float point = 0f;
			return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Tip, 26f * BladeScale, ref point);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// A flash of sparks where it hit; the finisher shakes the screen.
			for (int i = 0; i < 8; i++)
				Dust.NewDustPerfect(target.Center, Specter ? DustID.DungeonSpirit : DustID.GoldFlame, Main.rand.NextVector2Circular(6f, 6f), 0, default, 1.4f).noGravity = true;
			if (!Specter)
				ScreenShake.Shake(target.Center, Step == 2 ? 5f : 2f);
		}

		// The specter's body: the Bone Ronin's sprite (in the player's chosen look), see-through blue, its shoulder
		// where the katana turns. It raises its arm for the finisher.
		private void DrawSpecterBody(float opacity)
		{
			string path = "TerraIndex/Content/Bosses/BoneRonin/" + (RoninLook.Ember ? "BoneRonin_Ember" : "BoneRonin");
			Texture2D body = ModContent.Request<Texture2D>(path).Value;
			int frame = Step == 2 && Progress > Windup
				? RoninFrames.CastStart + (int)(Main.GameUpdateCount / 5 % RoninFrames.CastFrames)
				: RoninFrames.IdleStart + (int)(Main.GameUpdateCount / 7 % RoninFrames.IdleFrames);
			Rectangle source = new(0, frame * RoninFrames.Height, RoninFrames.Width, RoninFrames.Height);
			Vector2 shoulder = RoninFrames.Shoulder[frame];
			Vector2 position = Projectile.Center - new Vector2(shoulder.X * Side, shoulder.Y) - Main.screenPosition;
			SpriteEffects effects = Side == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			Vector2 origin = new(RoninFrames.Width / 2f, RoninFrames.Height / 2f);
			// A soft glow around it, then the body.
			for (int i = 0; i < 4; i++) {
				Vector2 offset = Vector2.UnitX.RotatedBy(MathHelper.PiOver2 * i + Main.GameUpdateCount * 0.05f) * 2f;
				Main.EntitySpriteDraw(body, position + offset, source, new Color(SpecterBlue.R, SpecterBlue.G, SpecterBlue.B, 0) * 0.3f * opacity, 0f, origin, 1f, effects, 0);
			}
			Main.EntitySpriteDraw(body, position, source, new Color(SpecterBlue.R, SpecterBlue.G, SpecterBlue.B, 110) * opacity, 0f, origin, 1f, effects, 0);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Projectile[Type].Value;
			Vector2 center = Projectile.Center - Main.screenPosition;
			float length = BladeLength * BladeScale;
			float opacity = Specter ? SpecterOpacity : 1f;
			if (Specter) {
				DrawSpecterBody(opacity);
				if (Timer <= 0)
					return false;
			}

			// The smear: a fan of light between this frame's blade and the last ones, brightest at the edge.
			for (int i = 0; i < oldAngles.Count - 1; i++) {
				float a0 = oldAngles[i], a1 = oldAngles[i + 1];
				float diff = a1 - a0;
				if (Math.Abs(diff) < 0.02f)
					continue;
				int lines = (int)Math.Ceiling(Math.Abs(diff) / 0.03f);
				for (int k = 0; k < lines; k++) {
					float angle = a0 + diff * k / lines;
					float fade = 1f - (i + k / (float)lines) / oldAngles.Count;
					Vector2 dir = angle.ToRotationVector2();
					BoneRonin.DrawLine(Main.spriteBatch, center + dir * length * 0.3f, center + dir * length, new Color(120, 200, 255, 0) * fade * 0.35f * opacity, 3f);
					BoneRonin.DrawLine(Main.spriteBatch, center + dir * length * 0.85f, center + dir * length * 1.04f, (Specter ? new Color(190, 230, 255, 0) : new Color(255, 225, 150, 0)) * fade * 0.8f * opacity, 3f);
				}
			}

			bool flip = Side == -1;
			SpriteEffects effects = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			Vector2 origin = flip ? new Vector2(texture.Width - Grip.X, Grip.Y) : Grip;
			float rotation = Projectile.rotation + (flip ? MathHelper.Pi * 0.75f : MathHelper.PiOver4);
			// Afterimages of the blade, then the blade, then a glow over it while it cuts.
			for (int i = oldAngles.Count - 1; i > 0; i -= 2) {
				float fade = 1f - i / (float)oldAngles.Count;
				float oldRotation = oldAngles[i] + (flip ? MathHelper.Pi * 0.75f : MathHelper.PiOver4);
				Main.EntitySpriteDraw(texture, center, null, new Color(140, 210, 255, 0) * fade * 0.4f * opacity, oldRotation, origin, BladeScale, effects, 0);
			}
			Main.EntitySpriteDraw(texture, center, null, (Specter ? new Color(SpecterBlue.R, SpecterBlue.G, SpecterBlue.B, 120) * opacity : Color.Lerp(lightColor, Color.White, 0.6f)), rotation, origin, BladeScale, effects, 0);
			if (Cutting)
				Main.EntitySpriteDraw(texture, center, null, (Specter ? new Color(170, 220, 255, 0) : new Color(255, 230, 170, 0)) * 0.5f * opacity, rotation, origin, BladeScale * 1.04f, effects, 0);
			// A glint on the point as the cut begins.
			float glint = 1f - Math.Abs(Progress - Windup) / 0.12f;
			if (glint > 0f) {
				Texture2D bloom = BoneRonin.Bloom;
				Vector2 tip = Tip - Main.screenPosition;
				Main.EntitySpriteDraw(bloom, tip, null, new Color(255, 240, 200, 0) * glint * opacity, 0f, bloom.Size() / 2f, new Vector2(1.6f, 0.1f) * glint, SpriteEffects.None, 0);
				Main.EntitySpriteDraw(bloom, tip, null, new Color(255, 240, 200, 0) * glint * opacity, MathHelper.PiOver2, bloom.Size() / 2f, new Vector2(1.6f, 0.1f) * glint, SpriteEffects.None, 0);
			}
			return false;
		}
	}

	// Right click: iaido. ai[1], ai[2]: the dash, from where the player stands. The projectile stays where the dash
	// starts; localAI[1], localAI[2] hold where it ended.
	public class RoninKatanaIaido : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override string Texture => "TerraIndex/Content/Bosses/BoneRonin/RoninKatana";

		public const float MaxDash = 480f;
		private const int Stance = 18, DashTime = 8, Sheathe = 18, CutTime = 22;
		private const int CutAt = Stance + DashTime + Sheathe;
		private const int HurtTicks = 6;

		private ref float Timer => ref Projectile.localAI[0];
		private Vector2 Dash => new(Projectile.ai[1], Projectile.ai[2]);
		private Vector2 End {
			get => new(Projectile.localAI[1], Projectile.localAI[2]);
			set { Projectile.localAI[1] = value.X; Projectile.localAI[2] = value.Y; }
		}

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
		}

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.timeLeft = CutAt + CutTime;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];
			if (!player.active || player.dead) {
				Projectile.Kill();
				return;
			}
			Timer++;
			Projectile.velocity = Vector2.Zero;
			Vector2 dir = Dash.SafeNormalize(Vector2.UnitX);
			if (Timer <= CutAt) {
				player.heldProj = Projectile.whoAmI;
				player.itemTime = player.itemAnimation = 2;
				player.ChangeDir(dir.X >= 0 ? 1 : -1);
			}

			if (Timer <= Stance) {
				// The stance: still, hand on the hilt at the hip.
				player.velocity = Vector2.Zero;
				Projectile.Center = player.MountedCenter;
				End = Projectile.Center + Dash;
				player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, player.direction * 0.6f);
				if (Timer == Stance - 10)
					SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.8f, Volume = 0.5f }, player.Center);
				return;
			}
			if (Timer <= Stance + DashTime) {
				// The dash: fast, through everything, untouchable.
				if (Timer == Stance + 1) {
					SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f }, player.Center);
					ScreenShake.Shake(player.Center, 3f);
				}
				player.velocity = Dash / DashTime;
				player.immune = true;
				player.immuneNoBlink = true;
				player.immuneTime = Math.Max(player.immuneTime, 24);
				player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, dir.ToRotation() - MathHelper.PiOver2);
				End = player.MountedCenter;
				if (!Main.dedServ)
					for (int i = 0; i < 3; i++)
						Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 20f), DustID.IceTorch, -dir * 2f, 0, default, 1.4f).noGravity = true;
				return;
			}
			if (Timer == Stance + DashTime + 1)
				player.velocity = dir * 3f;
			if (Timer == CutAt) {
				// The blade clicks back into the scabbard, and the cut lands.
				SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.4f }, player.Center);
				SoundEngine.PlaySound(SoundID.Item60, Vector2.Lerp(Projectile.Center, End, 0.5f));
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.6f }, Vector2.Lerp(Projectile.Center, End, 0.5f));
				ScreenShake.Shake(player.Center, 9f);
				if (!Main.dedServ)
					for (float t = 0f; t <= 1f; t += 0.025f)
						Dust.NewDustPerfect(Vector2.Lerp(Projectile.Center, End, t), Main.rand.NextBool() ? DustID.GoldFlame : DustID.IceTorch, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.6f).noGravity = true;
			}
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (Timer < CutAt || Timer >= CutAt + HurtTicks)
				return false;
			float point = 0f;
			return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, End, 80f, ref point);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// A cross of light on each enemy cut.
			for (int i = 0; i < 14; i++) {
				Vector2 along = (i % 2 == 0 ? new Vector2(1, 1) : new Vector2(1, -1)) * Main.rand.NextFloat(-7f, 7f);
				Dust.NewDustPerfect(target.Center, DustID.GoldFlame, along, 0, default, 1.6f).noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 start = Projectile.Center - Main.screenPosition;
			Vector2 end = End - Main.screenPosition;
			if (Timer <= Stance) {
				// Where you will land: a thin line that sharpens, and a glint at the hilt.
				float progress = Timer / Stance;
				BoneRonin.DrawLine(Main.spriteBatch, start, end, new Color(255, 225, 150, 0) * (0.15f + 0.35f * progress), 6f - 4f * progress);
				BoneRonin.DrawLine(Main.spriteBatch, start, end, new Color(255, 255, 255, 0) * (0.2f + 0.5f * progress), 1.5f);
				float glint = (float)Math.Sin(progress * MathHelper.Pi);
				Texture2D bloom = BoneRonin.Bloom;
				Vector2 hip = Main.player[Projectile.owner].MountedCenter + new Vector2(Main.player[Projectile.owner].direction * 8f, 6f) - Main.screenPosition;
				Main.EntitySpriteDraw(bloom, hip, null, new Color(255, 240, 200, 0) * glint, 0f, bloom.Size() / 2f, new Vector2(1.4f, 0.1f) * glint, SpriteEffects.None, 0);
				Main.EntitySpriteDraw(bloom, hip, null, new Color(255, 240, 200, 0) * glint, MathHelper.PiOver2, bloom.Size() / 2f, new Vector2(1.4f, 0.1f) * glint, SpriteEffects.None, 0);
			}
			else if (Timer < CutAt) {
				// The dash's streak, fading while the blade is sheathed.
				float fade = 1f - (Timer - Stance) / (CutAt - Stance) * 0.6f;
				BoneRonin.DrawLine(Main.spriteBatch, start, end, new Color(120, 200, 255, 0) * 0.35f * fade, 10f);
				BoneRonin.DrawLine(Main.spriteBatch, start, end, new Color(255, 255, 255, 0) * 0.7f * fade, 2f);
			}
			else {
				// The cut: a wide flash along the line that narrows and fades.
				float left = 1f - (Timer - CutAt) / CutTime;
				BoneRonin.DrawLine(Main.spriteBatch, start, end, new Color(120, 200, 255, 0) * left, 56f * left);
				BoneRonin.DrawLine(Main.spriteBatch, start, end, new Color(255, 230, 160, 0) * left, 22f * left);
				BoneRonin.DrawLine(Main.spriteBatch, start, end, new Color(255, 255, 255, 0) * left, 6f * left);
			}
			return false;
		}
	}

	// The specter: a see-through blue Ronin floating behind and above the player that repeats each swing of the combo
	// a moment later, for half the damage, crescents included (see RoninKatanaSwing).
	public class RoninKatanaSpecter : RoninKatanaSwing
	{
		protected override bool Specter => true;
	}

	// A phantom katana summoned by the katana's hits. ai[0]: delay before it falls; ai[1]: the enemy it goes for.
	public class RoninKatanaBlade : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override string Texture => "TerraIndex/Content/Bosses/BoneRonin/PhantomBlade";

		private const int Appear = 14;
		private ref float Timer => ref Projectile.localAI[0];
		private int Launch => Appear + (int)Projectile.ai[0];

		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = 2;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = -1;
			Projectile.timeLeft = 120;
			Projectile.alpha = 255;
		}

		public override void AI()
		{
			Timer++;
			NPC target = Main.npc[(int)Projectile.ai[1]];
			Vector2 goal = target.active ? target.Center : Projectile.Center + new Vector2(0, 300);
			if (Timer < Launch) {
				// Fades in pointing at the enemy.
				Projectile.alpha = Math.Max(40, Projectile.alpha - 20);
				Projectile.rotation = (goal - Projectile.Center).ToRotation() - MathHelper.PiOver2;
				return;
			}
			if (Timer == Launch) {
				Projectile.velocity = Projectile.DirectionTo(goal) * 30f;
				SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f, Volume = 0.6f }, Projectile.Center);
			}
			Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
			Lighting.AddLight(Projectile.Center, 0.8f, 0.7f, 0.4f);
		}

		public override bool? CanDamage() => Timer >= Launch;

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Projectile[Type].Value;
			Texture2D bloom = BoneRonin.Bloom;
			Vector2 center = Projectile.Center - Main.screenPosition;
			Main.EntitySpriteDraw(bloom, center, null, new Color(255, 210, 120, 0) * 0.5f * Projectile.Opacity, Projectile.rotation, bloom.Size() / 2f, new Vector2(0.5f, 1.3f), SpriteEffects.None, 0);
			Main.EntitySpriteDraw(texture, center, null, new Color(255, 225, 170, 140) * Projectile.Opacity, Projectile.rotation, texture.Size() / 2f, 1f, SpriteEffects.None, 0);
			return false;
		}
	}

	// The katana's crescent: the boss's slash wave, golden, through 3 enemies, fading after a short flight. ai[0] = 1
	// is the finisher's: bigger, through everything, flying further.
	public class KatanaSlash : ModProjectile
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public override string Texture => "TerraIndex/Content/Bosses/BoneRonin/RoninSlashWave";

		private bool Big => Projectile.ai[0] == 1f;

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

		public override void OnSpawn(IEntitySource source)
		{
			if (!Big)
				return;
			Projectile.scale = 1.9f;
			Projectile.Resize(56, 56);
			Projectile.penetrate = -1;
			Projectile.timeLeft = 55;
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation();
			float fadeTicks = Big ? 16f : 12f;
			if (Projectile.timeLeft < fadeTicks)
				Projectile.alpha = (int)(255 * (1 - Projectile.timeLeft / fadeTicks));
			Lighting.AddLight(Projectile.Center, 0.8f, 0.65f, 0.2f);
			if (Big && Main.rand.NextBool(2))
				Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f), DustID.GoldFlame, Projectile.velocity * 0.2f, 0, default, 1.3f).noGravity = true;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			RoninSlashWave.DrawCrescent(Projectile, new Color(255, 220, 140, 0));
			return false;
		}
	}
}
