using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace TerraIndex.Content.NPCs
{
	// A town NPC that moves in once the Eye of Cthulhu is beaten and sells the useful combat potions for 1 silver each.
	// Its sprite is built on the Guide's 26 frames (tools/sprites/apothecary.js), so it animates like him. Throws toxic
	// flasks at enemies.
	[AutoloadHead]
	public class Apothecary : ModNPC
	{
		private const string ShopName = "Shop";

		// Everyday buff potions.
		private static readonly int[] Potions = {
			ItemID.IronskinPotion, ItemID.RegenerationPotion, ItemID.SwiftnessPotion, ItemID.EndurancePotion,
			ItemID.LifeforcePotion, ItemID.HeartreachPotion, ItemID.RagePotion, ItemID.WrathPotion, ItemID.InfernoPotion,
			ItemID.ThornsPotion, ItemID.TitanPotion, ItemID.ArcheryPotion, ItemID.AmmoReservationPotion,
			ItemID.MagicPowerPotion, ItemID.ManaRegenerationPotion, ItemID.SummoningPotion, ItemID.ObsidianSkinPotion,
			ItemID.FeatherfallPotion, ItemID.GravitationPotion, ItemID.HunterPotion,
			// Less common, still handy in a fight: spawn rate for farming, luck (better damage rolls; only the best one,
			// they don't stack), seeing in the dark and traps, invisibility, water and cold bosses, escaping, the
			// Restoration Potion (shorter potion sickness) and the Red Potion.
			ItemID.BattlePotion, ItemID.LuckPotionGreater, ItemID.NightOwlPotion, ItemID.TrapsightPotion,
			ItemID.InvisibilityPotion, ItemID.WaterWalkingPotion, ItemID.GillsPotion, ItemID.WarmthPotion,
			ItemID.RecallPotion, ItemID.WormholePotion, ItemID.RestorationPotion, ItemID.RedPotion,
		};

		// Shops show 39 items (the last slot is kept for selling), so the weaker healing, mana and flasks give way to the
		// better ones as the world goes on: 37 items before Hardmode, 38 after.
		private static readonly (int Item, Condition[] Conditions)[] Staged = {
			(ItemID.HealingPotion, new[] { Condition.PreHardmode }),
			(ItemID.GreaterHealingPotion, new[] { Condition.Hardmode, Condition.NotDownedCultist }),
			// Its fragments only drop from the Celestial Pillars, which come after the Lunatic Cultist.
			(ItemID.SuperHealingPotion, new[] { Condition.DownedCultist }),
			(ItemID.ManaPotion, new[] { Condition.PreHardmode }),
			(ItemID.GreaterManaPotion, new[] { Condition.Hardmode }),
			(ItemID.FlaskofFire, new[] { Condition.PreHardmode }),
			(ItemID.FlaskofPoison, new[] { Condition.PreHardmode }),
			(ItemID.FlaskofParty, new[] { Condition.PreHardmode }),
			(ItemID.FlaskofCursedFlames, new[] { Condition.Hardmode }),
			(ItemID.FlaskofIchor, new[] { Condition.Hardmode }),
			(ItemID.FlaskofVenom, new[] { Condition.Hardmode }),
			(ItemID.FlaskofGold, new[] { Condition.Hardmode }),
		};

		public override void SetStaticDefaults()
		{
			// The sprite is drawn on the Guide's frames, so it takes his frame layout.
			Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Guide];
			NPCID.Sets.ExtraFramesCount[Type] = NPCID.Sets.ExtraFramesCount[NPCID.Guide];
			NPCID.Sets.AttackFrameCount[Type] = NPCID.Sets.AttackFrameCount[NPCID.Guide];
			NPCID.Sets.DangerDetectRange[Type] = 600;
			NPCID.Sets.AttackType[Type] = 0;
			NPCID.Sets.AttackTime[Type] = 60;
			NPCID.Sets.AttackAverageChance[Type] = 30;
			NPCID.Sets.HatOffsetY[Type] = 4;

			NPC.Happiness
				.SetBiomeAffection<ForestBiome>(AffectionLevel.Like)
				.SetBiomeAffection<JungleBiome>(AffectionLevel.Love)
				.SetBiomeAffection<DesertBiome>(AffectionLevel.Dislike)
				.SetNPCAffection(NPCID.Dryad, AffectionLevel.Love)
				.SetNPCAffection(NPCID.Nurse, AffectionLevel.Like)
				.SetNPCAffection(NPCID.WitchDoctor, AffectionLevel.Like)
				.SetNPCAffection(NPCID.Demolitionist, AffectionLevel.Dislike)
				.SetNPCAffection(NPCID.ArmsDealer, AffectionLevel.Hate);
		}

		public override void SetDefaults()
		{
			NPC.townNPC = true;
			NPC.friendly = true;
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = NPCAIStyleID.Passive;
			NPC.damage = 10;
			NPC.defense = 15;
			NPC.lifeMax = 250;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.knockBackResist = 0.5f;
			AnimationType = NPCID.Guide;
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
				new FlavorTextBestiaryInfoElement(this.GetLocalizationKey("Bestiary")),
			});
		}

		public override bool CanTownNPCSpawn(int numTownNPCs) => NPC.downedBoss1;

		public override List<string> SetNPCNameList() => new() { "Elixa", "Morwen", "Tobias", "Fennel", "Salvia", "Corvin", "Brewster", "Isolde" };

		public override string GetChat()
		{
			var chat = new WeightedRandom<string>();
			for (int i = 0; i < 4; i++)
				chat.Add(this.GetLocalizedValue("Dialogue." + i));
			return chat;
		}

		public override void SetChatButtons(ref string button, ref string button2) => button = Language.GetTextValue("LegacyInterface.28");

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = ShopName;
		}

		public override void AddShops()
		{
			var shop = new NPCShop(Type, ShopName);
			foreach (int potion in Potions)
				shop.Add(new Item(potion) { shopCustomPrice = Item.buyPrice(silver: 1) });
			foreach (var (potion, conditions) in Staged)
				shop.Add(new Item(potion) { shopCustomPrice = Item.buyPrice(silver: 1) }, conditions);
			shop.Register();
		}

		public override void TownNPCAttackStrength(ref int damage, ref float knockback)
		{
			damage = 20;
			knockback = 4f;
		}

		public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown)
		{
			cooldown = 30;
			randExtraCooldown = 30;
		}

		public override void TownNPCAttackProj(ref int projType, ref int attackDelay)
		{
			projType = ProjectileID.ToxicFlask;
			attackDelay = 1;
		}

		public override void TownNPCAttackProjSpeed(ref float multiplier, ref float gravityCorrection, ref float randomOffset)
		{
			multiplier = 10f;
			gravityCorrection = 2f;
			randomOffset = 1f;
		}
	}
}
