using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using TerraIndex.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TerraIndex.Common
{
	public enum GuideSection
	{
		Armor,
		Weapons,
		// Rogue's two builds: stealth strikes, or throwing as fast as possible.
		WeaponsStealth,
		WeaponsSpam,
		// Accessories by what they are for, as the wiki sorts them.
		AccessoriesOffense,
		AccessoriesStealth,
		AccessoriesSpam,
		AccessoriesDefense,
		AccessoriesMobility,
		AccessoriesGeneral,
		Potions,
	}

	// The Calamity class guide (the recipe index's Guide tab, only with Calamity enabled): the gear the Calamity wiki's
	// "Class setups" guide recommends for the player's class at their point of the game. Data/CalamityGuide.txt is
	// generated from the wiki by tools/calamity/build_guide.py. The stage is the first boss of the guide's list not
	// beaten yet in this world (vanilla flags, and Calamity's through its "Downed" mod call).
	public class CalamityGuide : ModSystem
	{
		public static readonly string[] Classes = { "melee", "ranged", "magic", "summon", "rogue" };

		// The guide's stages, in order. pre-scal and pre-exo are added to pre-scal-exo once the other boss is beaten.
		private static readonly string[] Stages = { "pre-boss", "pre-evil1", "pre-evil2", "pre-skeletron", "pre-wof", "pre-mech",
			"post-mech1", "post-mech2", "pre-plantera", "pre-golem", "post-golem", "pre-lunar", "pre-moonlord", "pre-provi",
			"pre-polter", "pre-dog", "pre-yharon", "pre-scal-exo", "endgame" };
		private const int ScalExoStage = 17;

		// (stage, class) -> the items, in the guide's order, with their section.
		private static readonly Dictionary<(string Stage, string Class), List<(int Type, GuideSection Section)>> entries = new();
		private static Mod calamity;

		public static bool Enabled => calamity != null;

		public override void PostSetupContent()
		{
			entries.Clear();
			if (!ServerConfig.Instance.CalamityFeatures || !ModLoader.TryGetMod("CalamityMod", out calamity))
				return;
			using var reader = new StreamReader(new MemoryStream(Mod.GetFileBytes("Data/CalamityGuide.txt")));
			for (string line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
				string[] parts = line.Split('\t');
				if (line.StartsWith('#') || parts.Length < 4 || !Enum.TryParse(parts[2], true, out GuideSection section))
					continue;
				if (!entries.TryGetValue((parts[0], parts[1]), out var list))
					entries[(parts[0], parts[1])] = list = new();
				foreach (string reference in parts[3].Split(' ')) {
					int type = Resolve(reference);
					if (type > 0 && !list.Contains((type, section)))
						list.Add((type, section));
				}
			}
		}

		public override void Unload()
		{
			entries.Clear();
			CalamityBosses.Clear();
			calamity = null;
		}

		// "Terraria/GoldHelmet" or "CalamityMod/VictideHeadRogue" -> item type, or 0 if it doesn't exist (any more).
		private static int Resolve(string reference)
		{
			int slash = reference.IndexOf('/');
			if (slash < 0)
				return 0;
			string mod = reference[..slash], name = reference[(slash + 1)..];
			if (mod == "Terraria")
				return ItemID.Search.TryGetId(name, out int id) ? id : 0;
			return ModContent.TryFind(mod, name, out ModItem item) ? item.Type : 0;
		}

		internal static bool Downed(string boss)
		{
			try {
				return calamity.Call("Downed", boss) is true;
			}
			catch {
				return false;
			}
		}

		public static int StageCount => Stages.Length;

		// The current stage: before the first boss in the guide's order that hasn't been beaten in this world.
		public static int Stage()
		{
			if (!Downed("desertscourge"))
				return 0;
			if (!NPC.downedBoss2)
				return 1;
			if (!Downed("hivemind") && !Downed("perforator"))
				return 2;
			if (!NPC.downedBoss3)
				return 3;
			if (!Main.hardMode)
				return 4;
			int mechs = (NPC.downedMechBoss1 ? 1 : 0) + (NPC.downedMechBoss2 ? 1 : 0) + (NPC.downedMechBoss3 ? 1 : 0);
			if (mechs < 3)
				return 5 + mechs;
			if (!NPC.downedPlantBoss)
				return 8;
			if (!NPC.downedGolemBoss)
				return 9;
			// After Golem until one of the late Hardmode bosses the guide sends you to next.
			if (!Downed("plaguebringergoliath") && !Downed("ravager") && !NPC.downedFishron && !NPC.downedEmpressOfLight)
				return 10;
			if (!NPC.downedAncientCultist)
				return 11;
			if (!NPC.downedMoonlord)
				return 12;
			string[] calamityBosses = { "providence", "polterghast", "devourerofgods", "yharon" };
			for (int i = 0; i < calamityBosses.Length; i++) {
				if (!Downed(calamityBosses[i]))
					return 13 + i;
			}
			return Downed("exomechs") && Downed("supremecalamitas") ? 18 : ScalExoStage;
		}

		// The guide's items for a class at a stage, armor first, then weapons, accessories and potions. A rogue item can be
		// in both the stealth and the spam sections. A rogue item can be in
		// both the stealth and the spam sections.
		public static List<(int Type, GuideSection Section)> Items(string guideClass, int stage)
		{
			var items = new List<(int Type, GuideSection Section)>();
			if (guideClass == null || stage < 0 || stage >= Stages.Length)
				return items;
			var stages = new List<string> { Stages[stage] };
			if (stage == ScalExoStage) {
				// With one of the two beaten, the guide has a few extras for the other one.
				if (Downed("exomechs"))
					stages.Add("pre-scal");
				if (Downed("supremecalamitas"))
					stages.Add("pre-exo");
			}
			foreach (string id in stages) {
				if (entries.TryGetValue((id, guideClass), out var list))
					items.AddRange(list.Where(entry => !items.Contains(entry)));
			}
			return items.OrderBy(entry => entry.Section).ToList();
		}

		// The current items for the local player's class (none if they haven't picked one).
		public static List<int> CurrentItems() => Items(Main.LocalPlayer.GetModPlayer<CalamityGuidePlayer>().GuideClass, Stage()).Select(entry => entry.Type).Distinct().ToList();

		public static string ClassName(string guideClass) => guideClass switch {
			"melee" => UIHelpers.T("ClassMelee"),
			"ranged" => UIHelpers.T("ClassRanged"),
			"magic" => UIHelpers.T("ClassMagic"),
			"summon" => UIHelpers.T("ClassSummon"),
			"rogue" => UIHelpers.T("GuideRogue"),
			_ => UIHelpers.T("GuidePickClass"),
		};

		// The guide class of a damage class (Calamity's rogue included), or null.
		public static string ClassOf(DamageClass damageClass)
		{
			if (damageClass == DamageClass.Melee)
				return "melee";
			if (damageClass == DamageClass.Ranged)
				return "ranged";
			if (damageClass == DamageClass.Magic)
				return "magic";
			if (damageClass == DamageClass.Summon)
				return "summon";
			return damageClass.Mod?.Name == "CalamityMod" && damageClass.Name == "RogueDamageClass" ? "rogue" : null;
		}

		public static string StageName(int stage) => stage switch {
			0 => Before(Boss("DesertScourgeHead")),
			1 => Before(Vanilla(NPCID.EaterofWorldsHead) + " / " + Vanilla(NPCID.BrainofCthulhu)),
			2 => Before(Boss("HiveMind") + " / " + Boss("PerforatorHive")),
			3 => Before(Vanilla(NPCID.SkeletronHead)),
			4 => Before(Vanilla(NPCID.WallofFlesh)),
			5 => UIHelpers.T("GuideMech0"),
			6 => UIHelpers.T("GuideMech1"),
			7 => UIHelpers.T("GuideMech2"),
			8 => Before(Vanilla(NPCID.Plantera)),
			9 => Before(Vanilla(NPCID.Golem)),
			10 => UIHelpers.T("GuideAfter", Vanilla(NPCID.Golem)),
			11 => Before(Vanilla(NPCID.CultistBoss)),
			12 => Before(Vanilla(NPCID.MoonLordCore)),
			13 => Before(Boss("Providence")),
			14 => Before(Boss("Polterghast")),
			15 => Before(Boss("DevourerofGodsHead")),
			16 => Before(Boss("Yharon")),
			17 => Before(UIHelpers.T("GuideExoMechs") + " / " + Boss("SupremeCalamitas")),
			_ => UIHelpers.T("GuideEndgame"),
		};

		private static string Before(string bosses) => UIHelpers.T("GuideBefore", bosses);

		private static string Vanilla(int npc) => Lang.GetNPCNameValue(npc);

		private static string Boss(string name) => ModContent.TryFind("CalamityMod", name, out ModNPC npc) ? npc.DisplayName.Value : name;

		public static string SectionName(GuideSection section) => UIHelpers.T("GuideSection" + section);
	}

	// The class picked for the guide (saved with the character), and the chat message when a boss kill moves the
	// guide on to its next stage.
	public class CalamityGuidePlayer : ModPlayer
	{
		private int shownStage = -1;

		public string GuideClass { get; set; }

		public override void OnEnterWorld() => shownStage = -1;

		public void CycleClass(int step)
		{
			int index = Array.IndexOf(CalamityGuide.Classes, GuideClass);
			int count = CalamityGuide.Classes.Length;
			GuideClass = CalamityGuide.Classes[index < 0 ? (step > 0 ? 0 : count - 1) : ((index + step) % count + count) % count];
		}

		public override void PostUpdate()
		{
			if (!CalamityGuide.Enabled || Player.whoAmI != Main.myPlayer || Main.GameUpdateCount % 60 != 0)
				return;
			int stage = CalamityGuide.Stage();
			// The first check in a world only notes where it is; going back (another world) says nothing either.
			if (shownStage >= 0 && stage > shownStage)
				Announce(stage);
			shownStage = stage;
		}

		private void Announce(int stage)
		{
			string name = CalamityGuide.StageName(stage);
			if (GuideClass == null) {
				Main.NewText(UIHelpers.T("GuideNewStageNoClass", name), new Color(255, 200, 80));
				return;
			}
			var before = CalamityGuide.Items(GuideClass, shownStage).Select(entry => entry.Type).ToHashSet();
			var added = CalamityGuide.Items(GuideClass, stage).Where(entry => !before.Contains(entry.Type) && entry.Section != GuideSection.Potions)
				.DistinctBy(entry => entry.Type).ToList();
			string examples = string.Join(", ", added.Take(3).Select(entry => $"[i:{entry.Type}] {Lang.GetItemNameValue(entry.Type)}"));
			Main.NewText(UIHelpers.T("GuideNewStage", name, added.Count, CalamityGuide.ClassName(GuideClass), examples), new Color(255, 200, 80));
		}

		public override void SaveData(TagCompound tag)
		{
			if (GuideClass != null)
				tag["calamityGuideClass"] = GuideClass;
		}

		public override void LoadData(TagCompound tag)
		{
			string saved = tag.GetString("calamityGuideClass");
			GuideClass = Array.IndexOf(CalamityGuide.Classes, saved) >= 0 ? saved : null;
		}
	}

	// In the recipe index, the guide's items say what they are recommended as.
	public class CalamityGuideTooltip : GlobalItem
	{
		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (!CalamityGuide.Enabled || item.IsAir || !ModContent.GetInstance<RecipeIndexSystem>().IsOpen)
				return;
			string guideClass = Main.LocalPlayer.GetModPlayer<CalamityGuidePlayer>().GuideClass;
			foreach (var (type, section) in CalamityGuide.Items(guideClass, CalamityGuide.Stage())) {
				if (type == item.type) {
					tooltips.Add(new TooltipLine(Mod, "CalamityGuide", UIHelpers.T("GuideTooltip", CalamityGuide.SectionName(section), CalamityGuide.ClassName(guideClass)))
						{ OverrideColor = new Color(255, 200, 80) });
					return;
				}
			}
		}
	}
}
