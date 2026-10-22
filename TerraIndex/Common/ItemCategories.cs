using System;
using System.Collections.Generic;
using TerraIndex.UI;
using Terraria;
using Terraria.ID;

namespace TerraIndex.Common
{
	// Tabs of the item grids, in the order they are shown.
	public enum ItemCategory
	{
		All,
		Weapons,
		Tools,
		Armor,
		Accessories,
		Placeable,
		Consumables,
		Materials,
		Misc,
	}

	// Sorts every item into one tab, from its sample's stats. The first matching rule wins
	// (wings are accessories, wood is placeable rather than a material, ammo goes with the weapons).
	public static class ItemCategories
	{
		public static readonly ItemCategory[] Tabs = Enum.GetValues<ItemCategory>();

		private static readonly Dictionary<int, ItemCategory> cache = new();

		public static void ClearCache() => cache.Clear();

		public static ItemCategory Of(int type)
		{
			if (!cache.TryGetValue(type, out ItemCategory category))
				cache[type] = category = Classify(ContentSamples.ItemsByType[type]);
			return category;
		}

		private static ItemCategory Classify(Item item)
		{
			if (item.accessory)
				return ItemCategory.Accessories;
			if (item.headSlot >= 0 || item.bodySlot >= 0 || item.legSlot >= 0)
				return ItemCategory.Armor;
			if (item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.fishingPole > 0)
				return ItemCategory.Tools;
			if (item.damage > 0 && item.createTile < 0)
				return ItemCategory.Weapons;
			if (item.createTile >= 0 || item.createWall >= 0)
				return ItemCategory.Placeable;
			if (item.consumable || item.potion || item.healLife > 0 || item.healMana > 0)
				return ItemCategory.Consumables;
			if (item.material)
				return ItemCategory.Materials;
			return ItemCategory.Misc;
		}

		public static int Icon(ItemCategory category) => category switch {
			ItemCategory.Weapons => ItemID.IronBroadsword,
			ItemCategory.Tools => ItemID.IronPickaxe,
			ItemCategory.Armor => ItemID.IronChainmail,
			ItemCategory.Accessories => ItemID.HermesBoots,
			ItemCategory.Placeable => ItemID.WorkBench,
			ItemCategory.Consumables => ItemID.LesserHealingPotion,
			ItemCategory.Materials => ItemID.IronBar,
			ItemCategory.Misc => ItemID.FallenStar,
			_ => ItemID.Chest,
		};

		public static string Name(ItemCategory category) => UIHelpers.T("Category" + category);
	}
}
