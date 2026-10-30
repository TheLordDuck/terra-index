using System;
using System.Collections.Generic;
using System.Linq;
using TerraIndex.UI;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace TerraIndex.Common
{
	public enum GuideBossKind
	{
		Boss,
		Event,
		Miniboss,
	}

	// One row of the guide book's boss list: a boss (or a few that count as one, like the evil bosses or the Exo Mechs),
	// an event or a miniboss.
	public record GuideBoss
	{
		public GuideBossKind Kind { get; init; } = GuideBossKind.Boss;
		// 0: pre-Hardmode, 1: Hardmode, 2: after Moon Lord.
		public int Tier { get; init; }
		// The NPCs it is made of; the first one's map icon stands for it, unless it has an item for icon.
		public int[] Npcs { get; init; } = Array.Empty<int>();
		public int IconItem { get; init; }
		public Func<bool> IsDowned { get; init; }
		// Calamity's progression doesn't need it.
		public bool Optional { get; init; }
		// A name of its own, when the NPCs' names don't make one ("The Twins", events).
		public Func<string> CustomName { get; init; }

		// The bosses the progression goes through, the ones counted and shown as next.
		public bool Required => Kind == GuideBossKind.Boss && !Optional;

		public string Name => CustomName?.Invoke() ?? string.Join(" / ", Npcs.Select(Lang.GetNPCNameValue));
	}

	// Every boss of a Calamity playthrough, vanilla ones included, in the order the Calamity wiki's progression fights
	// them, with the events and minibosses where they come in, for the guide book: what is beaten in this world, what is
	// missing and what comes next. Calamity's ask its "Downed" mod call (CalamityGuide.Downed); vanilla ones use the
	// game's flags.
	public static class CalamityBosses
	{
		private static List<GuideBoss> all;

		public static List<GuideBoss> All => all ??= Build();

		public static void Clear() => all = null;

		private static List<GuideBoss> Build()
		{
			var bosses = new List<GuideBoss> {
				Vanilla(0, () => NPC.downedSlimeKing, NPCID.KingSlime),
				Calamity(0, "desertscourge", "DesertScourgeHead"),
				Calamity(0, "giantclam", "GiantClam") with { Kind = GuideBossKind.Miniboss },
				Vanilla(0, () => NPC.downedBoss1, NPCID.EyeofCthulhu),
				Event(0, "EventAcidRain1", () => CalamityGuide.Downed("acidrain1"), CalamityItem("CausticTear")),
				Calamity(0, "crabulon", "Crabulon"),
				Vanilla(0, () => NPC.downedBoss2, NPCID.EaterofWorldsHead, NPCID.BrainofCthulhu),
				Event(0, "EventGoblins", () => NPC.downedGoblins, ItemID.GoblinBattleStandard),
				Event(0, "EventOldOnesArmy1", () => DD2Event.DownedInvasionT1, ItemID.DD2ElderCrystal),
				// Either one opens the way on, like the vanilla evil bosses.
				new GuideBoss { Tier = 0, Npcs = Npcs("HiveMind", "PerforatorHive"), IsDowned = () => CalamityGuide.Downed("hivemind") || CalamityGuide.Downed("perforator") },
				Vanilla(0, () => NPC.downedQueenBee, NPCID.QueenBee),
				Vanilla(0, () => NPC.downedDeerclops, NPCID.Deerclops) with { Optional = true },
				Vanilla(0, () => NPC.downedBoss3, NPCID.SkeletronHead),
				Calamity(0, "slimegod", "SlimeGodCore"),
				Vanilla(0, () => Main.hardMode, NPCID.WallofFlesh),

				Vanilla(1, () => NPC.downedQueenSlime, NPCID.QueenSlimeBoss),
				Event(1, "EventPirates", () => NPC.downedPirates, ItemID.PirateMap),
				Event(1, "EventFrostLegion", () => NPC.downedFrost, ItemID.SnowGlobe),
				Calamity(1, "cryogen", "Cryogen"),
				Vanilla(1, () => NPC.downedMechBoss1, NPCID.TheDestroyer),
				Vanilla(1, () => NPC.downedMechBoss2, NPCID.Retinazer) with { CustomName = () => Terraria.Localization.Language.GetTextValue("Enemies.TheTwins") },
				Vanilla(1, () => NPC.downedMechBoss3, NPCID.SkeletronPrime),
				Event(1, "EventOldOnesArmy2", () => DD2Event.DownedInvasionT2, ItemID.DD2ElderCrystal),
				Calamity(1, "aquaticscourge", "AquaticScourgeHead"),
				Event(1, "EventAcidRain2", () => CalamityGuide.Downed("acidrain2"), CalamityItem("CausticTear")),
				Calamity(1, "cragmawmire", "CragmawMire") with { Kind = GuideBossKind.Miniboss },
				Calamity(1, "brimstoneelemental", "BrimstoneElemental"),
				Calamity(1, "calamitasclone", "CalamitasClone"),
				Vanilla(1, () => NPC.downedPlantBoss, NPCID.Plantera),
				Calamity(1, "greatsandshark", "GreatSandShark") with { Kind = GuideBossKind.Miniboss },
				Event(1, "EventPumpkinMoon", () => NPC.downedHalloweenKing, ItemID.PumpkinMoonMedallion),
				Event(1, "EventFrostMoon", () => NPC.downedChristmasIceQueen, ItemID.NaughtyPresent),
				Calamity(1, "leviathan", "Leviathan", "Anahita"),
				Calamity(1, "astrumaureus", "AstrumAureus"),
				Vanilla(1, () => NPC.downedGolemBoss, NPCID.Golem),
				Event(1, "EventOldOnesArmy3", () => DD2Event.DownedInvasionT3, ItemID.DD2ElderCrystal),
				Event(1, "EventMartians", () => NPC.downedMartians, 0, NPCID.MartianSaucerCore),
				Calamity(1, "plaguebringergoliath", "PlaguebringerGoliath"),
				Vanilla(1, () => NPC.downedEmpressOfLight, NPCID.HallowBoss) with { Optional = true },
				Vanilla(1, () => NPC.downedFishron, NPCID.DukeFishron) with { Optional = true },
				Calamity(1, "ravager", "RavagerBody"),
				Vanilla(1, () => NPC.downedAncientCultist, NPCID.CultistBoss),
				Event(1, "EventPillars", () => NPC.downedTowerSolar && NPC.downedTowerVortex && NPC.downedTowerNebula && NPC.downedTowerStardust,
					0, NPCID.LunarTowerSolar),
				Calamity(1, "astrumdeus", "AstrumDeusHead"),
				Vanilla(1, () => NPC.downedMoonlord, NPCID.MoonLordCore),

				Calamity(2, "profanedguardians", "ProfanedGuardianCommander"),
				Calamity(2, "dragonfolly", "Dragonfolly"),
				Calamity(2, "providence", "Providence"),
				Calamity(2, "ceaselessvoid", "CeaselessVoid"),
				Calamity(2, "stormweaver", "StormWeaverHead"),
				Calamity(2, "signus", "Signus"),
				Calamity(2, "polterghast", "Polterghast"),
				// The third tier of the Acid Rain: its minibosses, and the Old Duke ends it.
				Calamity(2, "mauler", "Mauler") with { Kind = GuideBossKind.Miniboss },
				Calamity(2, "nuclearterror", "NuclearTerror") with { Kind = GuideBossKind.Miniboss },
				Calamity(2, "oldduke", "OldDuke") with { Optional = true },
				Calamity(2, "devourerofgods", "DevourerofGodsHead"),
				Calamity(2, "yharon", "Yharon"),
				Calamity(2, "exomechs", "ThanatosHead", "AresBody", "Apollo") with { CustomName = () => UIHelpers.T("BookExoMechs") },
				Calamity(2, "supremecalamitas", "SupremeCalamitas"),
				Calamity(2, "primordialwyrm", "PrimordialWyrmHead") with { Optional = true },
				Event(2, "EventBossRush", () => CalamityGuide.Downed("bossrush"), CalamityItem("Terminus")),
			};
			// Something a Calamity update renamed away is left out rather than shown as its internal name.
			return bosses.Where(boss => boss.Npcs.Length > 0 || boss.IconItem > 0).ToList();
		}

		private static GuideBoss Vanilla(int tier, Func<bool> downed, params int[] npcs) => new() { Tier = tier, Npcs = npcs, IsDowned = downed };

		private static GuideBoss Calamity(int tier, string downedKey, params string[] npcs) =>
			new() { Tier = tier, Npcs = Npcs(npcs), IsDowned = () => CalamityGuide.Downed(downedKey) };

		// An event, shown with an item that starts it (or an NPC's map icon), named by the "Book" + nameKey text.
		private static GuideBoss Event(int tier, string nameKey, Func<bool> downed, int iconItem, params int[] npcs) => new() {
			Kind = GuideBossKind.Event, Tier = tier, IconItem = iconItem, Npcs = npcs, IsDowned = downed,
			CustomName = () => UIHelpers.T("Book" + nameKey),
		};

		private static int[] Npcs(params string[] names) =>
			names.Select(name => ModContent.TryFind("CalamityMod", name, out ModNPC npc) ? npc.Type : 0).Where(type => type > 0).ToArray();

		private static int CalamityItem(string name) => ModContent.TryFind("CalamityMod", name, out ModItem item) ? item.Type : 0;
	}
}
