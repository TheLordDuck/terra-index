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
	// Below half life he stops, becomes invulnerable for a moment and enters phase 2: faster and harder, and his echo
	// (RoninEcho) steps out of him to repeat each of his attacks a moment later. Below 5% he
	// does it again and enters devil mode (phase 3): the echo flies back into him, he turns red and everything is much
	// faster (shorter waits and telegraphs, more dashes, volleys, cuts and blades): a duel, just him and the player.
	// A blow that would kill him before then sends him there instead.
	// He also has an entrance (fades in, raises the sword, roars) and a death animation (light breaks out of him).
	//
	// Animation: the body sheet has an animation per action (see FindFrame and tools/sprites/ronin.js); the sword arm is a separate
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

		// He is a player's size. The sprites, and where the shoulder and the eye are in each frame, come from
		// tools/sprites/ronin.js (see RoninFrames). The body sheet has an animation for each thing he does: idle (cloth
		// rippling), dash (leaning, everything blown back), iaido (crouched, a hand on the scabbard), cast (arm raised,
		// cloth rising) and dying (cracks glowing, pieces breaking off).
		private const float DrawScale = 1f;
		private int frameIndex;
		// Draws the frame so his feet are at the bottom of the hitbox.
		private Vector2 DrawOffset => new(0, NPC.height / 2f - RoninFrames.FeetY);

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
		// 0 = normal, 1 = below half life, 2 = below 5% life (devil mode: everything much faster, red).
		private int Phase {
			get => (int)NPC.ai[3];
			set => NPC.ai[3] = value;
		}
		private bool PhaseTwo => Phase >= 1;
		private bool PhaseThree => Phase >= 2;
		private T ByPhase<T>(T one, T two, T three) => PhaseThree ? three : PhaseTwo ? two : one;

		private static readonly Color DevilRed = new(255, 45, 35);
		private const float PhaseThreeLife = 0.05f;

		// The echo (RoninEcho): in phase 2 only, a see-through blue copy of him that repeats each of his attacks a moment
		// later, aimed at the player and from the other side. It can't be hurt; when he goes into devil mode it flies
		// back into him, and when he dies or leaves it fades away.
		// It is this same class with IsEcho on, so it moves, animates and attacks exactly like him.
		protected virtual bool IsEcho => false;
		private static readonly Color EchoBlue = new(110, 175, 255);
		// Echo, server only: the attacks to repeat, and the tick each one becomes due.
		private readonly Queue<(State attack, uint at)> echoQueue = new();
		// Echo: the Bone Ronin it copies (found again every tick).
		private BoneRonin owner;
		private const int EchoDelay = 60; // 1 second

		private static BoneRonin FindActive<T>() where T : BoneRonin
		{
			int type = ModContent.NPCType<T>();
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc.active && npc.type == type)
					return npc.ModNPC as BoneRonin;
			}
			return null;
		}

		// Iaido: where the cut ends (synced in SendExtraAI).
		private Vector2 cutEnd;
		// Server only: where the blade rain's lanes are.
		private float rainAnchor;
		// Server only: the last attack, so it isn't picked twice in a row.
		private State lastAttack = State.Hover;

		// The red line of the cut shows for at least 0.6 s: enough to run or jump out of it with starting gear.
		private int IaidoFade => PhaseThree ? 20 : 30;
		private int IaidoTelegraph => ByPhase(50, 42, 36);
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
			Main.npcFrameCount[Type] = RoninFrames.Count;
			NPCID.Sets.MPAllowedEnemies[Type] = true;
			if (IsEcho)
				NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true };
			else
				NPCID.Sets.BossBestiaryPriority.Add(Type);
			NPCID.Sets.TrailCacheLength[Type] = 10;
			NPCID.Sets.TrailingMode[Type] = 3;
			NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
			NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
		}

		public override void SetDefaults()
		{
			// A little bigger than a player's 20 x 42.
			NPC.width = 24;
			NPC.height = 46;
			// The last boss, after Supreme Calamitas: 1.5 million life (2.4 M in Expert, 3.6 M in Master) and hits
			// for Calamity endgame numbers.
			NPC.damage = 300;
			NPC.defense = 120;
			NPC.lifeMax = 1500000;
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
			NPC.value = Item.buyPrice(platinum: 5);
			if (!Main.dedServ)
				Music = MusicID.LunarBoss;
			if (IsEcho) {
				NPC.boss = false;
				NPC.npcSlots = 0f;
				NPC.value = 0;
				NPC.dontTakeDamage = true;
				Music = -1;
				// The echo hits softer (Expert and Master scale this along with the rest).
				NPC.damage = (int)(NPC.damage * 0.7f);
			}
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
			// Calamity's best healing potion, like its other endgame bosses.
			potionType = ModContent.TryFind("CalamityMod", "OmegaHealingPotion", out ModItem omega) ? omega.Type : ItemID.SuperHealingPotion;
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
			// A blow big enough to skip the last phase: it goes into devil mode instead of dying.
			if (!PhaseThree && AIState is not (State.Dying or State.Awaken)) {
				Phase = 1;
				NPC.life = 1;
				SetState(State.Awaken);
				NPC.alpha = 0;
				return false;
			}
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
			return NPC.alpha < 100 && AIState is not (State.Intro or State.Awaken or State.Dying or State.Leave);
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
		// The numbers passed are what a hit does in Expert (before the player's defense); Normal hits for 70% of it
		// and Master for 130%. The echo's attacks hit softer.
		private int ProjectileDamage(int damage)
		{
			float mode = Main.masterMode ? 1.3f : Main.expertMode ? 1f : 0.7f;
			int hit = (int)(damage * mode) / (Main.masterMode ? 6 : Main.expertMode ? 4 : 2);
			return IsEcho ? Math.Max(1, (int)(hit * 0.7f)) : hit;
		}

		// ---------------------------------------------------------------------------------------------------------
		// AI

		public override void AI()
		{
			// The echo follows the Bone Ronin's target and phase, and leaves with him.
			if (IsEcho) {
				owner = FindActive<BoneRonin>();
				// Devil mode (or him going into it) ends the echo.
				bool devil = owner != null && (owner.Phase >= 2 || (owner.AIState == State.Awaken && owner.Counter >= 2));
				if (owner != null && !devil && owner.AIState is not (State.Dying or State.Leave)) {
					NPC.target = owner.NPC.target;
					Phase = owner.Phase;
				}
				else if (AIState != State.Leave)
					SetState(State.Leave);
			}

			if (AIState != State.Dying) {
				if (NPC.target < 0 || NPC.target == 255 || Target.dead || !Target.active)
					NPC.TargetClosest();
				if (AIState != State.Leave && (Target.dead || !Target.active || NPC.Distance(Target.Center) > 6000f)) {
					NPC.TargetClosest();
					if (Target.dead || !Target.active || NPC.Distance(Target.Center) > 6000f)
						SetState(State.Leave);
				}
			}

			bool fighting = AIState is State.Hover or State.Dash or State.Waves or State.Iaido or State.BladeRain;
			bool nextPhase = Phase == 0 ? NPC.life < NPC.lifeMax / 2 : Phase == 1 && NPC.life < NPC.lifeMax * PhaseThreeLife;
			if (fighting && nextPhase && !IsEcho) {
				SetState(State.Awaken);
				NPC.alpha = 0;
			}

			Timer++;
			NPC.dontTakeDamage = IsEcho;
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

			if (IsEcho) {
				// Blue spirit wisps rising off the echo.
				Lighting.AddLight(NPC.Center, EchoBlue.ToVector3() * 0.5f * NPC.Opacity);
				if (!Main.dedServ && NPC.alpha < 150 && Main.rand.NextBool(2)) {
					Dust wisp = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(12f, 24f), DustID.DungeonSpirit, new Vector2(0, -1.2f), 100, default, 0.9f);
					wisp.noGravity = true;
				}
				return;
			}
			Lighting.AddLight(NPC.Center, (PhaseThree ? DevilRed.ToVector3() : RoninLook.LightVector) * NPC.Opacity);
			// Phase 2: the eye trails a blue flame. Devil mode: a red one, and red embers come off his body.
			if (PhaseTwo && !Main.dedServ && NPC.alpha < 100 && Main.rand.NextBool(PhaseThree ? 1 : 2)) {
				int eyeDust = PhaseThree ? DustID.RedTorch : RoninLook.Dust;
				Dust dust = Dust.NewDustPerfect(EyePosition, eyeDust, new Vector2(-NPC.spriteDirection * 1.5f, -1.2f), 0, default, PhaseThree ? 1.8f : 1.2f);
				dust.noGravity = true;
				if (PhaseThree) {
					Dust ember = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(14f, 26f), DustID.RedTorch, new Vector2(0, -2.5f), 0, default, 1.4f);
					ember.noGravity = true;
				}
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

		// To one side of the player and above; the echo takes the side opposite the Bone Ronin, so they flank the player.
		private Vector2 HoverSpot {
			get {
				bool right = IsEcho && owner != null ? owner.NPC.Center.X < Target.Center.X : Target.Center.X < NPC.Center.X;
				return Target.Center + new Vector2(right ? 430 : -430, IsEcho ? -160 : -220);
			}
		}

		private void Intro(ref float arm)
		{
			NPC.dontTakeDamage = true;
			arm = -MathHelper.PiOver2;
			if (Timer == 1 && IsServer) {
				// The echo comes out of the Bone Ronin.
				NPC.Center = IsEcho && owner != null ? owner.NPC.Center : Target.Center + new Vector2(0, -280);
				NPC.velocity = IsEcho ? new Vector2(0, -3f) : Vector2.Zero;
				NPC.netUpdate = true;
			}
			if (IsEcho) {
				if (Timer == 1)
					SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f }, NPC.Center);
				FacePlayer();
				NPC.velocity *= 0.95f;
				NPC.alpha = Math.Max(0, NPC.alpha - 6);
				if (Timer == 40)
					Glint();
				if (Timer >= 60)
					SetState(State.Hover);
				return;
			}
			// Fades in while he powers up.
			NPC.alpha = Math.Max(0, NPC.alpha - 3);
			PowerUp(ref arm, false);
			if (Timer >= PowerUpTime) {
				StartSwing(1.1f, 10);
				ScreenShake.Shake(NPC.Center, 5f);
				SetState(State.Hover);
			}
		}

		// The 3-second power-up of his entrance and of both phase changes: he kneels while sparks gather into him
		// (faster and faster, with tremors and a rising hum), rises with the katana raised to the sky while sparks
		// swirl round its point, then roars (the phase changes here) and holds the pose before striking down.
		private const int PowerUpTime = 180, RiseAt = 80, RoarAt = 130;

		private void PowerUp(ref float arm, bool devil)
		{
			NPC.dontTakeDamage = true;
			NPC.velocity *= 0.9f;
			FacePlayer();
			int dustType = devil ? DustID.RedTorch : RoninLook.Dust;
			if (Timer == 1)
				SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.8f }, NPC.Center);

			if (Timer < RiseAt) {
				// Kneeling, the blade held low behind him.
				arm = 2.7f;
				float k = Timer / RiseAt;
				if (!Main.dedServ) {
					for (int i = 0; i < 1 + (int)(k * 3); i++) {
						Vector2 from = NPC.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(80f, 200f) * (1.2f - k * 0.4f);
						Dust dust = Dust.NewDustPerfect(from, dustType, (NPC.Center - from) * 0.06f, 0, default, 1.2f + k);
						dust.noGravity = true;
					}
				}
				if (Timer % 20 == 0) {
					ScreenShake.Shake(NPC.Center, 1f + k * 3f);
					SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.6f + k, Volume = 0.5f }, NPC.Center);
				}
				return;
			}
			arm = -MathHelper.PiOver2;
			if (Timer == RiseAt) {
				StartSwing(-MathHelper.PiOver2, 20);
				Ring(24, 5f, dustType);
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.4f }, NPC.Center);
			}
			if (Timer < RoarAt) {
				// Sparks swirling into the raised blade's point.
				if (!Main.dedServ && Timer > RiseAt + 12) {
					Vector2 tip = Tip;
					Vector2 from = tip + Main.rand.NextVector2Unit() * Main.rand.NextFloat(20f, 50f);
					Dust dust = Dust.NewDustPerfect(from, dustType, (tip - from).RotatedBy(0.6f) * 0.1f, 0, default, 1.4f);
					dust.noGravity = true;
				}
				if (Timer == RoarAt - 15)
					Glint();
				return;
			}
			if (Timer == RoarAt) {
				SoundEngine.PlaySound(SoundID.Roar with { Pitch = devil ? -0.6f : 0f }, NPC.Center);
				ScreenShake.Shake(NPC.Center, devil ? 16f : 12f);
				Ring(60, 12f, dustType);
				Ring(30, 6f, dustType);
			}
			if (Timer == RoarAt + 25)
				Ring(40, 16f, dustType);
			// Power pouring off him while he holds the pose.
			if (!Main.dedServ && Main.rand.NextBool(2)) {
				Dust dust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(14f, 26f), dustType, Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), 0, default, 1.5f);
				dust.noGravity = true;
			}
		}

		private void Hover()
		{
			NPC.alpha = Math.Max(0, NPC.alpha - 15);
			FacePlayer();
			MoveTo(HoverSpot, ByPhase(14f, 18f, 26f), ByPhase(20f, 20f, 12f));
			if (!IsServer)
				return;
			if (IsEcho) {
				// Repeats the next attack once it is due; ones it fell too far behind on are skipped.
				while (echoQueue.Count > 0 && Main.GameUpdateCount > echoQueue.Peek().at + 180)
					echoQueue.Dequeue();
				if (Timer >= 10 && echoQueue.Count > 0 && Main.GameUpdateCount >= echoQueue.Peek().at)
					SetState(echoQueue.Dequeue().attack);
				return;
			}
			// A short breather between attacks, even in devil mode, to reposition.
			if (Timer >= ByPhase(80, 50, 32))
				SetState(PickAttack());
		}

		private State PickAttack()
		{
			var options = new List<State> { State.Dash, State.Waves, State.Iaido };
			if (PhaseTwo)
				options.Add(State.BladeRain);
			options.Remove(lastAttack);
			lastAttack = options[Main.rand.Next(options.Count)];
			// The echo will do the same a moment later.
			FindActive<RoninEcho>()?.echoQueue.Enqueue((lastAttack, Main.GameUpdateCount + (uint)EchoDelay));
			return lastAttack;
		}

		// Fairness rules, for every attack and phase: each one is announced (glint, sound, a line where it will land)
		// at least ~0.35 s before it can hit; he never attacks from right on top of the player; patterns always leave
		// a gap the player fits through; and repeated hits have a short gap between them.
		private const float MinAttackDistance = 260f;

		private int DashWindup => ByPhase(32, 24, 22);
		private int DashLength => PhaseThree ? 15 : 18;

		private void Dash(ref float arm)
		{
			int windup = DashWindup, DashTime = DashLength, Recover = PhaseThree ? 12 : 14;
			// The echo does one dash fewer, so the two together stay readable.
			int dashes = ByPhase(3, 4, 5) - (IsEcho ? 1 : 0);

			if (Timer < windup) {
				arm = -2.1f;
				FacePlayer();
				// Too close to dash fairly: backs off while winding up.
				if (NPC.Distance(Target.Center) < MinAttackDistance)
					NPC.velocity = Vector2.Lerp(NPC.velocity, Target.DirectionTo(NPC.Center) * 12f, 0.2f);
				else
					NPC.velocity *= 0.9f;
				if (Timer == windup - 16)
					Glint();
			}
			else if (Timer == windup) {
				StartSwing(1.3f, 9);
				SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
				ScreenShake.Shake(NPC.Center, 3f);
				if (IsServer) {
					// Aims a little ahead of where the player is going.
					NPC.velocity = NPC.DirectionTo(Target.Center + Target.velocity * 10f) * ByPhase(26f, 32f, 36f);
					NPC.netUpdate = true;
				}
			}
			else if (Timer < windup + DashTime) {
				arm = 1.3f;
				NPC.direction = NPC.velocity.X >= 0 ? 1 : -1;
				// Phase 2: two slash waves fly out sideways from the middle of the dash (not when that is right beside
				// the player, where they couldn't be seen coming).
				if (PhaseTwo && Timer == windup + DashTime / 2 && IsServer && NPC.Distance(Target.Center) > 160f) {
					Vector2 side = NPC.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * (PhaseThree ? 9f : 7f);
					int type = ModContent.ProjectileType<RoninSlashWave>();
					Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, side, type, ProjectileDamage(420), 0f);
					Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, -side, type, ProjectileDamage(420), 0f);
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
			int interval = ByPhase(40, 30, 24);
			int volleys = ByPhase(3, 4, 5) - (IsEcho ? 1 : 0);
			FacePlayer();
			// Keeps his distance: from close up a fan has no gaps.
			MoveTo(HoverSpot, NPC.Distance(Target.Center) < MinAttackDistance ? 16f : 8f, 20f);
			arm = -1.7f;

			if (Timer % interval == interval - 12 && Timer < interval * volleys)
				Glint();
			if (Timer % interval == 0 && Timer <= interval * volleys) {
				StartSwing(1.2f, 8);
				SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);
				if (IsServer) {
					int count = ByPhase(3, 5, 5);
					float distance = Math.Max(NPC.Distance(Target.Center), 120f);
					Vector2 aim = NPC.DirectionTo(Target.Center) * (PhaseThree ? 14f : 13f);
					// The waves are spread so that, where the player is, there is always a gap of about 3 player
					// widths between two of them. Every other volley is shifted by half a step, so the waves alternate
					// between hitting where the player is and passing beside it: a rhythm to read (move, stay, move).
					float step = MathHelper.Clamp((float)Math.Atan2(40f + 64f, distance), MathHelper.ToRadians(12f), MathHelper.ToRadians(28f));
					float shift = (int)(Timer / interval) % 2 == 0 ? 0f : 0.5f;
					for (int i = 0; i < count; i++) {
						float angle = step * (i - (count - 1) / 2f + shift);
						Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aim.RotatedBy(angle), ModContent.ProjectileType<RoninSlashWave>(), ProjectileDamage(420), 0f);
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
				NPC.alpha = Math.Min(255, NPC.alpha + (PhaseThree ? 13 : 9));
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
					Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<IaidoCut>(), ProjectileDamage(620), 0f, -1, cutEnd.X, cutEnd.Y);
					NPC.Center = cutEnd;
					NPC.netUpdate = true;
				}
			}
			arm = -0.8f;
			// A moment to recover between cuts. The echo cuts once.
			if (Timer >= IaidoStrike + (PhaseThree ? 18 : 25)) {
				Counter++;
				if (Counter < (IsEcho ? 1 : ByPhase(1, 2, 3))) {
					Timer = 0;
					NPC.netUpdate = true;
				}
				else
					SetState(State.Hover);
			}
		}

		private void BladeRain(ref float arm)
		{
			// The blades fall in fixed lanes 100 px apart (set when the rain starts), sweeping left to right; the last
			// one falls where the player is. Between two lanes there is always room to stand. Devil mode: faster, and
			// after a short pause a second sweep comes back right to left in the same lanes. The echo uses the Bone
			// Ronin's lanes, so its rain hits the same lanes again instead of filling the gaps.
			int Rise = PhaseThree ? 24 : 40, Blades = PhaseThree ? 24 : 12, Every = PhaseThree ? 4 : 6;
			int pauseSteps = PhaseThree ? 6 : 0;
			int steps = Blades + pauseSteps;
			FacePlayer();
			MoveTo(Target.Center + new Vector2(0, -360), PhaseThree ? 24f : 16f, 15f);
			// The katana raised to the sky while the blades appear.
			arm = -MathHelper.PiOver2;
			if (Timer == Rise - 10)
				Glint();

			int t = (int)(Timer - Rise);
			if (t > 0 && t % Every == 0 && t / Every <= steps && IsServer) {
				int step = t / Every - 1;
				int index = step < 12 ? step : step - pauseSteps;
				if (step < 12 || step >= 12 + pauseSteps) {
					if (index == 0)
						rainAnchor = IsEcho && owner != null ? owner.rainAnchor : Target.Center.X;
					int slot = index % 12;
					int lane = index < 12 ? slot : 10 - slot;
					float x = slot == 11 ? Target.Center.X : rainAnchor - 550f + lane * 100f + Main.rand.NextFloat(-15f, 15f);
					Vector2 spot = new(x, Target.Center.Y - 460f);
					Projectile.NewProjectile(NPC.GetSource_FromAI(), spot, Vector2.Zero, ModContent.ProjectileType<PhantomBlade>(), ProjectileDamage(480), 0f);
				}
			}
			if (Timer == Rise + steps * Every + 20) {
				StartSwing(1.0f, 10);
				ScreenShake.Shake(NPC.Center, 4f);
			}
			if (Timer >= Rise + steps * Every + (PhaseThree ? 30 : 60))
				SetState(State.Hover);
		}

		private void Awaken(ref float arm)
		{
			// Counter holds the phase he is going into (set on the first tick, so the phase can change mid-way).
			if (Timer == 1)
				Counter = Phase + 1;
			// Into devil mode (from phase 2): red sparks, a deeper roar.
			bool devil = Counter >= 2;
			PowerUp(ref arm, devil);
			if (Timer == RoarAt) {
				Phase = (int)Counter;
				string key = devil ? "Mods.TerraIndex.Messages.RoninFrenzy" : "Mods.TerraIndex.Messages.RoninAwakens";
				Color color = devil ? DevilRed : RoninLook.Main;
				if (Main.netMode == NetmodeID.Server)
					ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key), color);
				else if (Main.netMode == NetmodeID.SinglePlayer)
					Main.NewText(Language.GetTextValue(key), color);
				if (devil)
					SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
				// Phase 2: his echo steps out of him (devil mode is a duel: no echo).
				if (!devil && IsServer && FindActive<RoninEcho>() == null)
					NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Bottom.Y, ModContent.NPCType<RoninEcho>(), 0, 0f, 0f, 0f, Phase, NPC.target);
			}
			if (Timer >= PowerUpTime) {
				StartSwing(1.2f, 10);
				ScreenShake.Shake(NPC.Center, devil ? 10f : 6f);
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
				Dust dust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(14f, 26f), RoninLook.Dust, Main.rand.NextVector2Circular(4f, 4f) * (1 + progress * 2), 0, default, 1.6f);
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
						Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(14f, 26f), DustID.Bone, Main.rand.NextVector2Circular(8f, 8f), 0, default, 1.4f);
				}
				if (IsServer)
					NPC.StrikeInstantKill();
			}
		}

		private void Leave()
		{
			// The echo flies back into the Bone Ronin (devil mode), fading as it goes, and vanishes in a burst on him.
			if (IsEcho && owner != null && owner.AIState is not (State.Dying or State.Leave)) {
				MoveTo(owner.NPC.Center, 16f, 6f);
				NPC.alpha = Math.Min(255, NPC.alpha + 4);
				if (!Main.dedServ && Main.rand.NextBool(2)) {
					Dust wisp = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(12f, 24f), DustID.DungeonSpirit, -NPC.velocity * 0.2f, 100, default, 1.1f);
					wisp.noGravity = true;
				}
				if (NPC.Distance(owner.NPC.Center) < 20f || NPC.alpha >= 255) {
					Ring(30, 6f, DustID.DungeonSpirit);
					SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.6f }, NPC.Center);
					if (IsServer) {
						NPC.active = false;
						NPC.netUpdate = true;
					}
				}
				return;
			}
			NPC.velocity.Y -= 0.5f;
			NPC.alpha = Math.Min(255, NPC.alpha + 5);
			NPC.EncourageDespawn(10);
			if ((Timer > 300 || (IsEcho && NPC.alpha >= 255)) && IsServer) {
				NPC.active = false;
				NPC.netUpdate = true;
			}
		}

		// A ring of blue sparks bursting out (client only).
		private void Ring(int count, float speed) => Ring(count, speed, RoninLook.Dust);

		private void Ring(int count, float speed, int dustType)
		{
			if (Main.dedServ)
				return;
			for (int i = 0; i < count; i++) {
				Vector2 velocity = Vector2.UnitX.RotatedBy(MathHelper.TwoPi * i / count) * speed;
				Dust.NewDustPerfect(NPC.Center, dustType, velocity, 0, default, 2f).noGravity = true;
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

		private Vector2 Bob => new(0, (float)Math.Sin(Main.GameUpdateCount * 0.06f) * 2.5f * DrawScale);

		// A point on the body given as an offset from the frame centre for facing right: mirrored when facing left,
		// turned with the lean.
		private Vector2 BodyPoint(Vector2 offset) => NPC.Center + Bob + DrawOffset + (offset * DrawScale * new Vector2(NPC.spriteDirection, 1)).RotatedBy(NPC.rotation);

		private int Frame => Math.Clamp(frameIndex, 0, RoninFrames.Count - 1);
		private Vector2 Shoulder => BodyPoint(RoninFrames.Shoulder[Frame]);
		private Vector2 EyePosition => BodyPoint(RoninFrames.Eye[Frame]);

		private float WorldAngle(float local) => (NPC.spriteDirection == 1 ? local : MathHelper.Pi - local) + NPC.rotation;

		private Vector2 Tip => Shoulder + WorldAngle(armAngle).ToRotationVector2() * RoninFrames.ArmLength * DrawScale;

		// Picks the animation for what he is doing; devil mode plays them faster.
		public override void FindFrame(int frameHeight)
		{
			int start = RoninFrames.IdleStart, count = RoninFrames.IdleFrames;
			float ticks = 7f;
			switch (AIState) {
				case State.Intro:
				case State.Awaken:
					// Power-up: kneeling, then rising with the katana raised.
					if (Timer < RiseAt)
						(start, count, ticks) = (RoninFrames.IaidoStart, RoninFrames.IaidoFrames, 8f);
					else
						(start, count, ticks) = (RoninFrames.CastStart, RoninFrames.CastFrames, 5f);
					break;
				case State.Dash:
					// Crouches while winding up, leans into the dash, then straightens up.
					if (Timer < DashWindup)
						(start, count, ticks) = (RoninFrames.IaidoStart, RoninFrames.IaidoFrames, 6f);
					else if (Timer < DashWindup + DashLength)
						(start, count, ticks) = (RoninFrames.DashStart, RoninFrames.DashFrames, 3f);
					break;
				case State.Iaido:
					(start, count, ticks) = (RoninFrames.IaidoStart, RoninFrames.IaidoFrames, 6f);
					break;
				case State.BladeRain:
					(start, count, ticks) = (RoninFrames.CastStart, RoninFrames.CastFrames, 5f);
					break;
				case State.Dying:
					frameIndex = RoninFrames.DyingStart + Math.Min(RoninFrames.DyingFrames - 1, (int)(Timer / DyingTime * RoninFrames.DyingFrames));
					NPC.frame.Y = frameIndex * frameHeight;
					return;
				default:
					if (NPC.velocity.Length() > 12f)
						(start, count, ticks) = (RoninFrames.DashStart, RoninFrames.DashFrames, 3f);
					break;
			}
			if (PhaseThree)
				ticks *= 0.6f;
			NPC.frameCounter++;
			frameIndex = start + (int)(NPC.frameCounter / ticks) % count;
			NPC.frame.Y = frameIndex * frameHeight;
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
			Vector2 position = NPC.Center - screenPos + Bob + DrawOffset;
			float opacity = NPC.Opacity;
			// He is a ghost: never fully dark.
			Color color = NPC.GetAlpha(Color.Lerp(drawColor, Color.White, 0.35f));

			if (AIState == State.Dying)
				DrawDeathLight(spriteBatch, position);
			if (AIState == State.Iaido && Timer > IaidoFade && Timer < IaidoStrike)
				DrawIaidoTelegraph(spriteBatch, screenPos);

			// The echo: see-through spectral blue, whatever the look or phase.
			if (IsEcho)
				color = new Color(EchoBlue.R, EchoBlue.G, EchoBlue.B, 110) * opacity;

			// Afterimages when moving fast (devil mode: red, and at lower speeds; the echo always leaves them).
			if (NPC.velocity.Length() > (IsEcho ? 2f : PhaseThree ? 4f : 8f)) {
				Color trail = IsEcho ? EchoBlue : PhaseThree ? DevilRed : RoninLook.Main;
				for (int i = NPC.oldPos.Length - 1; i > 0; i--) {
					float fade = (NPC.oldPos.Length - i) / (float)NPC.oldPos.Length;
					Vector2 old = NPC.oldPos[i] + NPC.Size / 2f - screenPos + Bob + DrawOffset;
					spriteBatch.Draw(body, old, NPC.frame, Light(trail, fade * 0.5f * opacity), NPC.rotation, origin, DrawScale, effects, 0f);
				}
			}
			// Phase 2: a pulsing blue aura around him. Devil mode: a bigger, faster red one.
			if (PhaseTwo) {
				float speed = PhaseThree ? 0.25f : 0.1f;
				float pulse = (PhaseThree ? 0.5f : 0.35f) + 0.15f * (float)Math.Sin(Main.GameUpdateCount * speed);
				float spread = PhaseThree ? 5f : 3f;
				Color aura = IsEcho ? EchoBlue : PhaseThree ? DevilRed : RoninLook.Soft;
				for (int i = 0; i < 4; i++) {
					Vector2 offset = Vector2.UnitX.RotatedBy(MathHelper.PiOver2 * i + Main.GameUpdateCount * speed * 0.5f) * spread;
					spriteBatch.Draw(body, position + offset, NPC.frame, Light(aura, pulse * opacity), NPC.rotation, origin, DrawScale, effects, 0f);
				}
			}

			// Devil mode tints him red.
			if (PhaseThree && !IsEcho)
				color = Color.Lerp(color, new Color(255, 120, 110, color.A), 0.45f);
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
			Vector2 armOrigin = RoninFrames.ArmOrigin;
			Vector2 origin = NPC.spriteDirection == -1 ? new Vector2(armOrigin.X, arm.Height - armOrigin.Y) : armOrigin;

			// Slash arc over the swept angle, fading out.
			if (arcTime > 0) {
				Texture2D arc = arcTexture.Value;
				float strength = arcTime / (float)arcLength;
				float middle = WorldAngle((arcFrom + arcTo) / 2f);
				float scale = (RoninFrames.ArmLength + 12f) / (arc.Width / 2f) * DrawScale;
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
				spriteBatch.Draw(bloom, tip, null, Light(RoninLook.Bright, g * opacity), 0f, center, 0.6f * g * DrawScale, SpriteEffects.None, 0f);
				spriteBatch.Draw(bloom, tip, null, Light(255, 255, 255, g * opacity), 0f, center, new Vector2(1.6f, 0.1f) * g * DrawScale, SpriteEffects.None, 0f);
				spriteBatch.Draw(bloom, tip, null, Light(255, 255, 255, g * opacity), MathHelper.PiOver2, center, new Vector2(1.6f, 0.1f) * g * DrawScale, SpriteEffects.None, 0f);
			}
		}

		private void DrawEyeGlow(SpriteBatch spriteBatch, Vector2 screenPos, float opacity)
		{
			Texture2D bloom = Bloom;
			Vector2 eye = EyePosition - screenPos;
			float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GameUpdateCount * 0.15f);
			float size = ByPhase(0.2f, 0.3f, 0.45f) * DrawScale;
			spriteBatch.Draw(bloom, eye, null, Light(IsEcho ? EchoBlue : PhaseThree ? DevilRed : RoninLook.Main, pulse * opacity * 0.8f), 0f, bloom.Size() / 2f, size, SpriteEffects.None, 0f);
			spriteBatch.Draw(bloom, eye, null, Light(255, 255, 255, opacity), 0f, bloom.Size() / 2f, 0.06f * DrawScale, SpriteEffects.None, 0f);
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
				float length = 20f + progress * 300f * (0.55f + 0.45f * (i * 37 % 10) / 10f);
				DrawLine(spriteBatch, position, position + angle.ToRotationVector2() * length, Light(RoninLook.Bright, progress * 0.8f), 2f + progress * 8f);
			}
			Texture2D bloom = Bloom;
			spriteBatch.Draw(bloom, position, null, Light(RoninLook.Bright, progress), 0f, bloom.Size() / 2f, 0.6f + progress * 3.5f, SpriteEffects.None, 0f);
		}

		public static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
		{
			Vector2 delta = end - start;
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), color, delta.ToRotation(), new Vector2(0, 0.5f), new Vector2(delta.Length(), thickness), SpriteEffects.None, 0f);
		}
	}
}
