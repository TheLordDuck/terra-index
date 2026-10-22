using System;
using System.Collections.Generic;
using System.Linq;
using TerraIndex.UI;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TerraIndex.Common
{
	public enum SourceKind
	{
		Shop,
		Drop,
		Container,
	}

	// One way to obtain an item. Id is the NPC (Shop/Drop; 0 = any enemy) or the bag/crate item (Container).
	public record ItemSource(SourceKind Kind, int Id, string Detail, string Conditions);

	// Where items come from when they can't be crafted: NPC shops, enemy drops, and bags/crates/presents.
	// Built the first time it is needed, once every mod has registered its shops and drops.
	public class SourceDatabase : ModSystem
	{
		private static readonly List<ItemSource> Empty = new();
		private static Dictionary<int, List<ItemSource>> sources;

		private static readonly Dictionary<int, string> locations = new();
		private static GameCulture locationsCulture;

		public override void Unload()
		{
			sources = null;
			locations.Clear();
		}

		// Where an NPC spawns, from its Bestiary entry: biomes, events and time of day ("Underground Jungle, Night").
		public static string LocationOf(int npc)
		{
			if (npc <= 0)
				return "";
			if (locationsCulture != Language.ActiveCulture) {
				locations.Clear();
				locationsCulture = Language.ActiveCulture;
			}
			if (locations.TryGetValue(npc, out string location))
				return location;

			var names = new List<string>();
			try {
				foreach (IBestiaryInfoElement info in Main.BestiaryDB.FindEntryByNPCID(npc).Info) {
					if (info is FilterProviderInfoElement or ModBiomeBestiaryInfoElement)
						names.Add(Language.GetTextValue(((IFilterInfoProvider)info).GetDisplayNameKey()));
				}
			}
			catch (Exception) {
				// A missing or broken Bestiary entry only means no location is shown.
			}
			return locations[npc] = Join(names);
		}

		public static List<ItemSource> SourcesOf(int type)
		{
			if (sources == null)
				Build();
			return sources.TryGetValue(type, out var list) ? list : Empty;
		}

		public static string SourceName(ItemSource source) => source.Kind switch {
			SourceKind.Container => Lang.GetItemNameValue(source.Id),
			_ when source.Id == 0 => UIHelpers.T("AnyEnemy"),
			_ => Lang.GetNPCNameValue(source.Id),
		};

		private static void Build()
		{
			sources = new Dictionary<int, List<ItemSource>>();
			var seen = new HashSet<(int, ItemSource, string)>();

			// Several NPCs share a name (zombie variants, etc.): only one row per name and chance.
			void Add(int item, ItemSource source)
			{
				if (item <= 0 || !seen.Add((item, source with { Id = 0 }, SourceName(source))))
					return;
				if (!sources.TryGetValue(item, out var list))
					sources[item] = list = new List<ItemSource>();
				list.Add(source);
			}

			foreach (AbstractNPCShop shop in NPCShopDatabase.AllShops) {
				foreach (AbstractNPCShop.Entry entry in shop.ActiveEntries) {
					if (entry.Item == null || entry.Item.IsAir)
						continue;
					string conditions = Join(entry.Conditions.Select(condition => condition.Description.Value));
					Add(entry.Item.type, new ItemSource(SourceKind.Shop, shop.NpcType, Price(entry.Item), conditions));
				}
			}

			for (int npc = 1; npc < NPCLoader.NPCCount; npc++) {
				foreach (IItemDropRule rule in Main.ItemDropsDB.GetRulesForNPCID(npc, false))
					Report(rule, info => Add(info.itemId, new ItemSource(SourceKind.Drop, npc, Chance(info), Join(info.conditions))));
			}

			// Drops shared by every enemy (Souls of Light/Night, hearts...): the rules a Blue Slime gets on top of its own.
			var own = new HashSet<IItemDropRule>(Main.ItemDropsDB.GetRulesForNPCID(NPCID.BlueSlime, false));
			foreach (IItemDropRule rule in Main.ItemDropsDB.GetRulesForNPCID(NPCID.BlueSlime, true)) {
				if (!own.Contains(rule))
					Report(rule, info => Add(info.itemId, new ItemSource(SourceKind.Drop, 0, Chance(info), Join(info.conditions))));
			}

			for (int item = 1; item < ItemLoader.ItemCount; item++) {
				foreach (IItemDropRule rule in Main.ItemDropsDB.GetRulesForItemID(item))
					Report(rule, info => Add(info.itemId, new ItemSource(SourceKind.Container, item, Chance(info), Join(info.conditions))));
			}
		}

		private static void Report(IItemDropRule rule, Action<DropRateInfo> add)
		{
			var infos = new List<DropRateInfo>();
			try {
				rule.ReportDroprates(infos, new DropRateInfoChainFeed(1f));
			}
			catch (Exception) {
				// A broken rule from another mod shouldn't stop the index from loading.
				return;
			}
			foreach (DropRateInfo info in infos)
				add(info);
		}

		private static string Join(IEnumerable<IItemDropRuleCondition> conditions) =>
			Join(conditions?.Select(condition => condition.GetConditionDescription()));

		private static string Join(IEnumerable<string> texts)
		{
			if (texts == null)
				return "";
			return string.Join(" · ", texts.Where(text => !string.IsNullOrWhiteSpace(text)).Distinct());
		}

		private static string Chance(DropRateInfo info)
		{
			float percent = info.dropRate * 100f;
			string chance = percent >= 1f ? percent.ToString("0.##") + "%" : percent.ToString("0.###") + "%";
			if (info.stackMax > 1)
				chance = (info.stackMin == info.stackMax ? $"{info.stackMax}" : $"{info.stackMin}-{info.stackMax}") + " x " + chance;
			return chance;
		}

		private static string Price(Item item)
		{
			long price = item.shopCustomPrice ?? item.value;
			if (price <= 0)
				return "";
			if (item.shopSpecialCurrency == CustomCurrencyID.DefenderMedals)
				return $"{price} {Lang.GetItemNameValue(ItemID.DefenderMedal)}";
			if (item.shopSpecialCurrency != -1)
				return "";
			return UIHelpers.Coins(price);
		}
	}
}
