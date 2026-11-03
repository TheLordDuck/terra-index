using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using TerraIndex.Common;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The Bone Ronin: a post-Moon Lord boss, the ghost of a skeleton swordsman. He floats near the player and picks
	// one attack after another (never the same one twice in a row):
	// - Dash: raises his katana, then dashes at the player 3 times (4 in phase 2, which also leaves slash waves).
	// - Waves: volleys of crescent slash waves in a fan.
	// - Iaido: vanishes, reappears beside the player in a low stance, shows the line of his cut, then cuts along it in
	//   an instant (twice in phase 2).
	// - Blade rain (phase 2 only): phantom katanas appear above the player and fall.
	// Below half life he stops, becomes invulnerable for a moment and enters phase 2: faster and harder.
	// He also has an entrance (fades in, raises the sword, roars) and a death animation (light breaks out of him).
	//
	// Animation: the body sheet is an 8-frame idle loop (cloth, scarf and sleeves moving); the sword arm is a separate
	// texture rotated by code, so every swing is smooth: it eases from where the arm is to where the swing ends, leaves
	// a blue smear and a slash arc, and a glint on the tip warns before each strike. The body bobs, leans into its
	// movement, and the eye glows. All of this is client-side and follows the synced state (NPC.ai).
	// Random choices and teleports are made by the server only; the iaido end point is synced through SendExtraAI.
	[AutoloadBossHead]
	public class BoneRonin : ModNPC
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		private enum State { Intro, Hover, Dash, Waves, Iaido, BladeRain, Awaken, Dying, Leave }

		private const string Path = "TerraIndex/Content/Bosses/BoneRonin/";

		// From tools/sprites/make_sprites.py (pixels from the frame centre / in the arm texture).
		private static readonly Vector2 ShoulderOffset = new(7, -25);
		private static readonly Vector2 EyeOffset = new(5, -55);
		private static readonly Vector2 ArmOrigin = new(9, 15);
		private const float ArmLength = 124f;
		// Drawn 1.5x: the art is already doubled, so each art pixel is exactly 3 screen pixels (still crisp). The
		// offsets above are for scale 1 and are multiplied by DrawScale where they are used. NPC.scale stays 1, so the
		// hitbox is exactly NPC.width x NPC.height.
		private const float DrawScale = 1.5f;

		private static Asset<Texture2D> armTexture;
		// The ember look (see RoninLook): same layout as the ghost sprites.
		private static Asset<Texture2D> emberBodyTexture;
		private static Asset<Texture2D> emberArmTexture;
		private static Asset<Texture2D> bloomTexture;
		private static Asset<Texture2D> arcTexture;
		public static Texture2D Bloom => bloomTexture.Value;

		private State AIState {
			get => (State)NPC.ai[0];
			set => NPC.ai[0] = (float)value;
		}
		private ref float Timer => ref NPC.ai[1];
		private ref float Counter => ref NPC.ai[2];
		private bool PhaseTwo {
			get => NPC.ai[3] == 1f;
			set => NPC.ai[3] = value ? 1f : 0f;
		}

		// Iaido: where the cut ends (synced in SendExtraAI).
		private Vector2 cutEnd;
		// Server only: the last attack, so it isn't picked twice in a row.
		private State lastAttack = State.Hover;

		private const int IaidoFade = 30;
		private int IaidoTelegraph => PhaseTwo ? 36 : 50;
		private int IaidoStrike => IaidoFade + IaidoTelegraph;
		private const int DyingTime = 150;

		// Sword arm (client-side animation). Angles are local: 0 = straight forward, positive = downwards, so the same
		// numbers work facing either way (see WorldAngle).
		private float armAngle = 0.9f;
		private float swingFrom, swingTo;
		private int swingTime, swingLength;
		private readonly List<float> armTrail = new();
		private float arcFrom, arcTo;
		private int arcTime, arcLength;
		private int glintTime;
		private const int GlintLength = 18;

		private Player Target => Main.player[NPC.target];
		private static bool IsServer => Main.netMode != NetmodeID.MultiplayerClient;

		public override void Load()
		{
			if (Main.dedServ)
				return;
			armTexture = ModContent.Request<Texture2D>(Path + "BoneRonin_Arm");
			emberBodyTexture = ModContent.Request<Texture2D>(Path + "BoneRonin_Ember");
			emberArmTexture = ModContent.Request<Texture2D>(Path + "BoneRonin_Ember_Arm");
			bloomTexture = ModContent.Request<Texture2D>(Path + "Bloom");
			arcTexture = ModContent.Request<Texture2D>(Path + "SlashArc");
		}

		public override void Unload()
		{
			armTexture = emberBodyTexture = emberArmTexture = bloomTexture = arcTexture = null;
		}

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 8;
			NPCID.Sets.MPAllowedEnemies[Type] = true;
			NPCID.Sets.BossBestiaryPriority.Add(Type);
			NPCID.Sets.TrailCacheLength[Type] = 10;
			NPCID.Sets.TrailingMode[Type] = 3;
			NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
			NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
		}

		public override void SetDefaults()
		{
			NPC.width = 84;
			NPC.height = 195;
			NPC.damage = 170;
			NPC.defense = 90;
			NPC.lifeMax = 320000;
			NPC.HitSound = SoundID.NPCHit2;
			NPC.DeathSound = SoundID.NPCDeath2;
			NPC.knockBackResist = 0f;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.lavaImmune = true;
			NPC.boss = true;
			NPC.npcSlots = 15f;
			NPC.aiStyle = -1;
			NPC.alpha = 255;
			NPC.value = Item.buyPrice(gold: 50);
			if (!Main.dedServ)
				Music = MusicID.LunarBoss;
		}

		public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
		{
			NPC.lifeMax = (int)(NPC.lifeMax * balance * bossAdjustment * 0.8f);
			NPC.damage = (int)(NPC.damage * 0.8f);
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Times.NightTime,
				new FlavorTextBestiaryInfoElement("Mods.TerraIndex.Bestiary.BoneRonin"),
			});
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<BoneRoninBag>()));
			var notExpert = new LeadingConditionRule(new Conditions.NotExpert());
			notExpert.OnSuccess(ItemDropRule.Common(ModContent.ItemType<RoninKatana>()));
			npcLoot.Add(notExpert);
		}

		public override void BossLoot(ref int potionType)
		{
			potionType = ItemID.SuperHealingPotion;
		}

		public override void OnKill()
		{
			NPC.SetEventFlagCleared(ref RoninDownedSystem.downedBoneRonin, -1);
		}

		// The killing blow starts the death animation instead; it ends with StrikeInstantKill, which lands here again.
		public override bool CheckDead()
		{
			if (AIState == State.Dying && Timer >= DyingTime)
				return true;
			if (AIState != State.Dying)
				SetState(State.Dying);
			NPC.life = 1;
			NPC.dontTakeDamage = true;
			NPC.netUpdate = true;
			return false;
		}

		// Contact damage only while he is visible and fighting.
		public override bool CanHitPlayer(Player target, ref int cooldownSlot)
		{
			cooldownSlot = ImmunityCooldownID.Bosses;
			return NPC.alpha < 100 && AIState is not (State.Intro or State.Awaken or State.Dying);
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.WriteVector2(cutEnd);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			cutEnd = reader.ReadVector2();
		}

		private void SetState(State state)
		{
			AIState = state;
			Timer = 0;
			Counter = 0;
			NPC.netUpdate = true;
		}

		// Hostile projectile damage is multiplied by the game (x2 normal, x4 expert, x6 master): this undoes it, so
		// the numbers below are roughly what a hit does.
		private static int ProjectileDamage(int damage) => damage / (Main.masterMode ? 6 : Main.expertMode ? 4 : 2);

		// ---------------------------------------------------------------------------------------------------------
		// AI

		public override void AI()
		{
			if (AIState != State.Dying) {
				if (NPC.target < 0 || NPC.target == 255 || Target.dead || !Target.active)
					NPC.TargetClosest();
				if (AIState != State.Leave && (Target.dead || !Target.active || NPC.Distance(Target.Center) > 6000f)) {
					NPC.TargetClosest();
					if (Target.dead || !Target.active || NPC.Distance(Target.Center) > 6000f)
						SetState(State.Leave);
				}
			}

			bool fighting = AIState is State.Hover or State.Dash or State.Waves or State.Iaido;
			if (!PhaseTwo && fighting && NPC.life < NPC.lifeMax / 2) {
				SetState(State.Awaken);
				NPC.alpha = 0;
			}

			Timer++;
			NPC.dontTakeDamage = false;
			float arm = 0.9f + (float)Math.Sin(Main.GameUpdateCount * 0.05f) * 0.06f;
			switch (AIState) {
				case State.Intro: Intro(ref arm); break;
				case State.Hover: Hover(); break;
				case State.Dash: Dash(ref arm); break;
				case State.Waves: Waves(ref arm); break;
				case State.Iaido: Iaido(ref arm); break;
				case State.BladeRain: BladeRain(ref arm); break;
				case State.Awaken: Awaken(ref arm); break;
				case State.Dying: Dying(ref arm); break;
				case State.Leave: Leave(); break;
			}
			NPC.spriteDirection = NPC.direction;
			NPC.rotation = MathHelper.Lerp(NPC.rotation, MathHelper.Clamp(NPC.velocity.X * 0.015f, -0.3f, 0.3f), 0.15f);
			UpdateArm(arm);

			Lighting.AddLight(NPC.Center, RoninLook.LightVector * NPC.Opacity);
			// Phase 2: the eye trails a blue flame.
			if (PhaseTwo && !Main.dedServ && NPC.alpha < 100 && Main.rand.NextBool(2)) {
				Dust dust = Dust.NewDustPerfect(EyePosition, RoninLook.Dust, new Vector2(-NPC.spriteDirection * 1.5f, -1.2f), 0, default, 1.2f);
				dust.noGravity = true;
			}
		}

		private void FacePlayer() => NPC.direction = Target.Center.X > NPC.Center.X ? 1 : -1;

		private void MoveTo(Vector2 goal, float speed, float inertia)
		{
			Vector2 toGoal = goal - NPC.Center;
			if (toGoal.Length() > speed)
				toGoal = toGoal.SafeNormalize(Vector2.Zero) * speed;
			NPC.velocity = (NPC.velocity * (inertia - 1) + toGoal) / inertia;
		}

		private Vector2 HoverSpot => Target.Center + new Vector2(Target.Center.X < NPC.Center.X ? 430 : -430, -220);

		private void Intro(ref float arm)
		{
			NPC.dontTakeDamage = true;
			arm = -MathHelper.PiOver2;
			if (Timer == 1 && IsServer) {
				NPC.Center = Target.Center + new Vector2(0, -280);
				NPC.velocity = Vector2.Zero;
				NPC.netUpdate = true;
			}
			FacePlayer();
			NPC.velocity *= 0.9f;
			NPC.alpha = Math.Max(0, NPC.alpha - 4);
			// Blue sparks gathering into him while he appears.
			if (!Main.dedServ && Timer < 64) {
				for (int i = 0; i < 2; i++) {
					Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(90f, 170f);
					Dust dust = Dust.NewDustPerfect(from, RoninLook.Dust, (NPC.Center - from) * 0.07f, 0, default, 1.5f);
					dust.noGravity = true;
				}
			}
			if (Timer == 52)
				Glint();
			if (Timer == 72) {
				StartSwing(1.1f, 10);
				SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
				ScreenShake.Shake(NPC.Center, 9f);
				Ring(40, 9f);
			}
			if (Timer >= 100)
				SetState(State.Hover);
		}

		private void Hover()
		{
			NPC.alpha = Math.Max(0, NPC.alpha - 15);
			FacePlayer();
			MoveTo(HoverSpot, PhaseTwo ? 18f : 14f, 20f);
			if (Timer >= (PhaseTwo ? 50 : 80) && IsServer)
				SetState(PickAttack());
		}

		private State PickAttack()
		{
			var options = new List<State> { State.Dash, State.Waves, State.Iaido };
			if (PhaseTwo)
				options.Add(State.BladeRain);
			options.Remove(lastAttack);
			lastAttack = options[Main.rand.Next(options.Count)];
			return lastAttack;
		}

		private void Dash(ref float arm)
		{
			int windup = PhaseTwo ? 22 : 32;
			const int DashTime = 18, Recover = 14;
			int dashes = PhaseTwo ? 4 : 3;

			if (Timer < windup) {
				arm = -2.1f;
				FacePlayer();
				NPC.velocity *= 0.9f;
				if (Timer == windup - 14)
					Glint();
			}
			else if (Timer == windup) {
				StartSwing(1.3f, 9);
				SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
				ScreenShake.Shake(NPC.Center, 3f);
				if (IsServer) {
					// Aims a little ahead of where the player is going.
					NPC.velocity = NPC.DirectionTo(Target.Center + Target.velocity * 10f) * (PhaseTwo ? 32f : 26f);
					NPC.netUpdate = true;
				}
			}
			else if (Timer < windup + DashTime) {
				arm = 1.3f;
				NPC.direction = NPC.velocity.X >= 0 ? 1 : -1;
				// Phase 2: two slash waves fly out sideways from the middle of the dash.
				if (PhaseTwo && Timer == windup + DashTime / 2 && IsServer) {
					Vector2 side = NPC.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * 7f;
					int type = ModContent.ProjectileType<RoninSlashWave>();
					Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, side, type, ProjectileDamage(130), 0f);
					Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, -side, type, ProjectileDamage(130), 0f);
				}
			}
			else if (Timer < windup + DashTime + Recover) {
				arm = 1.2f;
				NPC.velocity *= 0.85f;
			}
			else {
				Counter++;
				Timer = 0;
				if (Counter >= dashes)
					SetState(State.Hover);
			}
		}

		private void Waves(ref float arm)
		{
			int interval = PhaseTwo ? 30 : 40;
			int volleys = PhaseTwo ? 4 : 3;
			FacePlayer();
			MoveTo(HoverSpot, 8f, 30f);
			arm = -1.7f;

			if (Timer % interval == interval - 12 && Timer < interval * volleys)
				Glint();
			if (Timer % interval == 0 && Timer <= interval * volleys) {
				StartSwing(1.2f, 8);
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);
				if (IsServer) {
					int count = PhaseTwo ? 5 : 3;
					Vector2 aim = NPC.DirectionTo(Target.Center) * 13f;
					for (int i = 0; i < count; i++) {
						float angle = MathHelper.ToRadians(12f) * (i - (count - 1) / 2f);
						Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aim.RotatedBy(angle), ModContent.ProjectileType<RoninSlashWave>(), ProjectileDamage(130), 0f);
					}
				}
			}
			if (Timer >= interval * (volleys + 1))
				SetState(State.Hover);
		}

		private void Iaido(ref float arm)
		{
			// Low stance, the blade held back by the hip, ready to draw.
			arm = 2.7f;
			if (Timer < IaidoFade) {
				NPC.velocity *= 0.9f;
				NPC.alpha = Math.Min(255, NPC.alpha + 9);
				NPC.dontTakeDamage = NPC.alpha > 200;
				return;
			}
			if (Timer == IaidoFade) {
				// Reappears to one side of the player and locks the line of the cut through where the player is now.
				if (IsServer) {
					float side = Main.rand.NextBool() ? 1f : -1f;
					NPC.Center = Target.Center + new Vector2(side * 440f, Main.rand.NextFloat(-160f, 60f));
					NPC.velocity = Vector2.Zero;
					cutEnd = NPC.Center + NPC.DirectionTo(Target.Center) * 900f;
					NPC.netUpdate = true;
				}
				SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
			}
			if (Timer < IaidoStrike) {
				NPC.velocity = Vector2.Zero;
				NPC.alpha = Math.Max(0, NPC.alpha - 25);
				NPC.direction = cutEnd.X > NPC.Center.X ? 1 : -1;
				if (Timer == IaidoStrike - 16)
					Glint();
				return;
			}
			if (Timer == IaidoStrike) {
				StartSwing(-0.8f, 6);
				SoundEngine.PlaySound(SoundID.Item60, NPC.Center);
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, NPC.Center);
				ScreenShake.Shake(NPC.Center, 11f);
				if (IsServer) {
					Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IaidoCut>(), ProjectileDamage(200), 0f, -1, cutEnd.X, cutEnd.Y);
					NPC.Center = cutEnd;
					NPC.netUpdate = true;
				}
			}
			arm = -0.8f;
			if (Timer >= IaidoStrike + 25) {
				Counter++;
				if (PhaseTwo && Counter < 2) {
					Timer = 0;
					NPC.netUpdate = true;
				}
				else
					SetState(State.Hover);
			}
		}

		private void BladeRain(ref float arm)
		{
			const int Rise = 40, Blades = 12, Every = 6;
			FacePlayer();
			MoveTo(Target.Center + new Vector2(0, -360), 16f, 15f);
			// The katana raised to the sky while the blades appear.
			arm = -MathHelper.PiOver2;
			if (Timer == Rise - 10)
				Glint();

			if (Timer > Rise && Timer <= Rise + Blades * Every && (Timer - Rise) % Every == 0) {
				int index = (int)(Timer - Rise) / Every - 1;
				if (IsServer) {
					// Spread across the player's screen; the last one falls right on them.
					float x = index == Blades - 1 ? 0f : -550f + index * 100f + Main.rand.NextFloat(-30f, 30f);
					Vector2 spot = Target.Center + new Vector2(x, -460f);
					Projectile.NewProjectile(NPC.GetSource_FromAI(), spot, Vector2.Zero, ModContent.ProjectileType<PhantomBlade>(), ProjectileDamage(150), 0f);
				}
			}
			if (Timer == Rise + Blades * Every + 20) {
				StartSwing(1.0f, 10);
				ScreenShake.Shake(NPC.Center, 4f);
			}
			if (Timer >= Rise + Blades * Every + 60)
				SetState(State.Hover);
		}

		private void Awaken(ref float arm)
		{
			NPC.dontTakeDamage = true;
			NPC.velocity *= 0.9f;
			arm = -MathHelper.PiOver2;
			if (Timer == 1) {
				SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
				ScreenShake.Shake(NPC.Center, 12f);
				Ring(60, 10f);
				const string Key = "Mods.TerraIndex.Messages.RoninAwakens";
				var color = RoninLook.Main;
				if (Main.netMode == NetmodeID.Server)
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey(Key), color);
				else if (Main.netMode == NetmodeID.SinglePlayer)
					Main.NewText(Language.GetTextValue(Key), color);
			}
			if (Timer == 60)
				Glint();
			if (Timer >= 90) {
				PhaseTwo = true;
				StartSwing(1.2f, 10);
				ScreenShake.Shake(NPC.Center, 8f);
				Ring(40, 14f);
				SetState(State.Hover);
			}
		}

		// Shakes harder and harder while light breaks out of him, then bursts.
		private void Dying(ref float arm)
		{
			NPC.dontTakeDamage = true;
			NPC.velocity *= 0.9f;
			arm = 1.7f;
			float progress = Timer / DyingTime;
			ScreenShake.Shake(NPC.Center, 1f + progress * 6f);
			if (!Main.dedServ && Main.rand.NextFloat() < 0.3f + progress) {
				Dust dust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(45f, 90f), RoninLook.Dust, Main.rand.NextVector2Circular(4f, 4f) * (1 + progress * 2), 0, default, 1.6f);
				dust.noGravity = true;
			}
			if (Timer % 30 == 0)
				SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.5f + progress }, NPC.Center);
			if (Timer == DyingTime) {
				if (!Main.dedServ) {
					SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
					ScreenShake.Shake(NPC.Center, 16f);
					Ring(80, 16f);
					for (int i = 0; i < 40; i++)
						Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(42f, 90f), DustID.Bone, Main.rand.NextVector2Circular(8f, 8f), 0, default, 1.4f);
				}
				if (IsServer)
					NPC.StrikeInstantKill();
			}
		}

		private void Leave()
		{
			NPC.velocity.Y -= 0.5f;
			NPC.alpha = Math.Min(255, NPC.alpha + 5);
			NPC.EncourageDespawn(10);
			if (Timer > 300 && IsServer) {
				NPC.active = false;
				NPC.netUpdate = true;
			}
		}

		// A ring of blue sparks bursting out (client only).
		private void Ring(int count, float speed)
		{
			if (Main.dedServ)
				return;
			for (int i = 0; i < count; i++) {
				Vector2 velocity = Vector2.UnitX.RotatedBy(MathHelper.TwoPi * i / count) * speed;
				Dust.NewDustPerfect(NPC.Center, RoninLook.Dust, velocity, 0, default, 2f).noGravity = true;
			}
		}

		// ---------------------------------------------------------------------------------------------------------
		// Sword arm animation

		// Swings the arm from where it is to `to` in `length` ticks, fast at first and slowing down at the end,
		// with a slash arc over the swept angle.
		private void StartSwing(float to, int length)
		{
			swingFrom = armAngle;
			swingTo = to;
			swingTime = 0;
			swingLength = length;
			arcFrom = swingFrom;
			arcTo = to;
			arcLength = arcTime = length + 10;
		}

		private void Glint()
		{
			glintTime = GlintLength;
			if (!Main.dedServ)
				SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.8f, Volume = 0.45f }, NPC.Center);
		}

		private void UpdateArm(float target)
		{
			if (swingTime < swingLength) {
				swingTime++;
				float t = swingTime / (float)swingLength;
				float eased = 1f - (float)Math.Pow(1f - t, 3);
				armAngle = MathHelper.Lerp(swingFrom, swingTo, eased);
			}
			else
				armAngle = MathHelper.Lerp(armAngle, target, 0.12f);
			armTrail.Insert(0, armAngle);
			if (armTrail.Count > 8)
				armTrail.RemoveAt(armTrail.Count - 1);
			if (glintTime > 0)
				glintTime--;
			if (arcTime > 0)
				arcTime--;
		}

		private Vector2 Bob => new(0, (float)Math.Sin(Main.GameUpdateCount * 0.06f) * 4f * DrawScale);

		// A point on the body given as an offset for facing right: mirrored when facing left, turned with the lean.
		private Vector2 BodyPoint(Vector2 offset) => NPC.Center + Bob + (offset * DrawScale * new Vector2(NPC.spriteDirection, 1)).RotatedBy(NPC.rotation);

		private Vector2 Shoulder => BodyPoint(ShoulderOffset);
		private Vector2 EyePosition => BodyPoint(EyeOffset);

		private float WorldAngle(float local) => (NPC.spriteDirection == 1 ? local : MathHelper.Pi - local) + NPC.rotation;

		private Vector2 Tip => Shoulder + WorldAngle(armAngle).ToRotationVector2() * ArmLength * DrawScale;

		public override void FindFrame(int frameHeight)
		{
			NPC.frameCounter++;
			NPC.frame.Y = (int)(NPC.frameCounter / 6 % Main.npcFrameCount[Type]) * frameHeight;
		}

		// ---------------------------------------------------------------------------------------------------------
		// Drawing. Colours with alpha 0 are drawn as light (added to what's behind).

		private static Color Light(int r, int g, int b, float strength) => new Color(r, g, b, 0) * strength;
		private static Color Light(Color color, float strength) => RoninLook.Additive(color, strength);

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Texture2D body = RoninLook.Ember ? emberBodyTexture.Value : TextureAssets.Npc[Type].Value;
			Vector2 origin = NPC.frame.Size() / 2f;
			SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
			Vector2 position = NPC.Center - screenPos + Bob;
			float opacity = NPC.Opacity;
			// He is a ghost: never fully dark.
			Color color = NPC.GetAlpha(Color.Lerp(drawColor, Color.White, 0.35f));

			if (AIState == State.Dying)
				DrawDeathLight(spriteBatch, position);
			if (AIState == State.Iaido && Timer > IaidoFade && Timer < IaidoStrike)
				DrawIaidoTelegraph(spriteBatch, screenPos);

			// Afterimages when moving fast.
			if (NPC.velocity.Length() > 8f) {
				for (int i = NPC.oldPos.Length - 1; i > 0; i--) {
					float fade = (NPC.oldPos.Length - i) / (float)NPC.oldPos.Length;
					Vector2 old = NPC.oldPos[i] + NPC.Size / 2f - screenPos + Bob;
					spriteBatch.Draw(body, old, NPC.frame, Light(RoninLook.Main, fade * 0.5f * opacity), NPC.rotation, origin, DrawScale, effects, 0f);
				}
			}
			// Phase 2: a pulsing blue aura around him.
			if (PhaseTwo) {
				float pulse = 0.35f + 0.15f * (float)Math.Sin(Main.GameUpdateCount * 0.1f);
				for (int i = 0; i < 4; i++) {
					Vector2 offset = Vector2.UnitX.RotatedBy(MathHelper.PiOver2 * i + Main.GameUpdateCount * 0.05f) * 3f;
					spriteBatch.Draw(body, position + offset, NPC.frame, Light(RoninLook.Soft, pulse * opacity), NPC.rotation, origin, DrawScale, effects, 0f);
				}
			}

			spriteBatch.Draw(body, position, NPC.frame, color, NPC.rotation, origin, DrawScale, effects, 0f);
			DrawArm(spriteBatch, screenPos, color, opacity);
			DrawEyeGlow(spriteBatch, screenPos, opacity);
			return false;
		}

		private void DrawArm(SpriteBatch spriteBatch, Vector2 screenPos, Color color, float opacity)
		{
			Texture2D arm = (RoninLook.Ember ? emberArmTexture : armTexture).Value;
			Vector2 shoulder = Shoulder - screenPos;
			// Flipped vertically when facing left, so the sleeve still hangs below the arm.
			SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
			Vector2 origin = NPC.spriteDirection == -1 ? new Vector2(ArmOrigin.X, arm.Height - ArmOrigin.Y) : ArmOrigin;

			// Slash arc over the swept angle, fading out.
			if (arcTime > 0) {
				Texture2D arc = arcTexture.Value;
				float strength = arcTime / (float)arcLength;
				float middle = WorldAngle((arcFrom + arcTo) / 2f);
				float scale = (ArmLength + 16f) / (arc.Width / 2f) * DrawScale;
				spriteBatch.Draw(arc, shoulder, null, Light(RoninLook.Bright, strength * opacity), middle, arc.Size() / 2f, scale, SpriteEffects.None, 0f);
			}
			// Smear: the arm's last positions, filled in between, when it moves fast.
			for (int i = 0; i < armTrail.Count - 1; i++) {
				float a0 = armTrail[i], a1 = armTrail[i + 1];
				if (Math.Abs(a1 - a0) < 0.04f)
					continue;
				for (int k = 0; k < 3; k++) {
					float angle = MathHelper.Lerp(a0, a1, k / 3f);
					float fade = 1f - (i + k / 3f) / armTrail.Count;
					spriteBatch.Draw(arm, shoulder, null, Light(RoninLook.Main, fade * 0.45f * opacity), WorldAngle(angle), origin, DrawScale, effects, 0f);
				}
			}
			spriteBatch.Draw(arm, shoulder, null, color, WorldAngle(armAngle), origin, DrawScale, effects, 0f);

			// Glint on the tip: a four-pointed star that grows and shrinks.
			if (glintTime > 0) {
				Texture2D bloom = Bloom;
				float g = (float)Math.Sin(glintTime / (float)GlintLength * MathHelper.Pi);
				Vector2 tip = Tip - screenPos;
				Vector2 center = bloom.Size() / 2f;
				spriteBatch.Draw(bloom, tip, null, Light(RoninLook.Bright, g * opacity), 0f, center, 0.9f * g * DrawScale, SpriteEffects.None, 0f);
				spriteBatch.Draw(bloom, tip, null, Light(255, 255, 255, g * opacity), 0f, center, new Vector2(2.2f, 0.12f) * g * DrawScale, SpriteEffects.None, 0f);
				spriteBatch.Draw(bloom, tip, null, Light(255, 255, 255, g * opacity), MathHelper.PiOver2, center, new Vector2(2.2f, 0.12f) * g * DrawScale, SpriteEffects.None, 0f);
			}
		}

		private void DrawEyeGlow(SpriteBatch spriteBatch, Vector2 screenPos, float opacity)
		{
			Texture2D bloom = Bloom;
			Vector2 eye = EyePosition - screenPos;
			float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GameUpdateCount * 0.15f);
			float size = (PhaseTwo ? 0.5f : 0.32f) * DrawScale;
			spriteBatch.Draw(bloom, eye, null, Light(RoninLook.Main, pulse * opacity * 0.8f), 0f, bloom.Size() / 2f, size, SpriteEffects.None, 0f);
			spriteBatch.Draw(bloom, eye, null, Light(255, 255, 255, opacity), 0f, bloom.Size() / 2f, 0.1f * DrawScale, SpriteEffects.None, 0f);
		}

		// The line of the coming cut: a faint wide band and a thin core, from red to white as the strike nears.
		private void DrawIaidoTelegraph(SpriteBatch spriteBatch, Vector2 screenPos)
		{
			float progress = (Timer - IaidoFade) / IaidoTelegraph;
			Vector2 start = NPC.Center - screenPos;
			Vector2 end = cutEnd - screenPos;
			Color core = Color.Lerp(new Color(255, 60, 60), Color.White, progress * progress);
			DrawLine(spriteBatch, start, end, Light(255, 60, 60, 0.15f + 0.25f * progress), 10f + 14f * progress);
			DrawLine(spriteBatch, start, end, new Color(core.R, core.G, core.B, 0) * (0.4f + 0.6f * progress), 1.5f + 2.5f * progress);
		}

		// Rays of light breaking out of him while he dies, longer and brighter towards the end.
		private void DrawDeathLight(SpriteBatch spriteBatch, Vector2 position)
		{
			float progress = Timer / DyingTime;
			for (int i = 0; i < 9; i++) {
				float angle = i * MathHelper.TwoPi / 9f + Timer * 0.015f + i * 0.3f;
				float length = 40f + progress * 420f * (0.55f + 0.45f * (i * 37 % 10) / 10f);
				DrawLine(spriteBatch, position, position + angle.ToRotationVector2() * length, Light(RoninLook.Bright, progress * 0.8f), 4f + progress * 12f);
			}
			Texture2D bloom = Bloom;
			spriteBatch.Draw(bloom, position, null, Light(RoninLook.Bright, progress), 0f, bloom.Size() / 2f, 1f + progress * 5f, SpriteEffects.None, 0f);
		}

		public static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
		{
			Vector2 delta = end - start;
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), color, delta.ToRotation(), new Vector2(0, 0.5f), new Vector2(delta.Length(), thickness), SpriteEffects.None, 0f);
		}
	}
}
