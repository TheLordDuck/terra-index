using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;

namespace TerraIndex.Common
{
	// The recipe index's "Next upgrades" tab: craftable gear better than what the player has, among what the bosses
	// beaten so far let them make. Rarity stands in for progression (vanilla gives later gear higher rarities), so each
	// boss raises the highest rarity suggested; modded rarities are above every vanilla one and never suggested.
	// Weapons: the strongest few of each damage class the player already uses, by damage per second. Armor: the
	// toughest pieces for each slot, against what is equipped. Tools: the strongest pickaxe.
	public static class Upgrades
	{
		private const int WeaponsPerClass = 3;
		private const int ArmorPerSlot = 2;
		// A weapon has to be this much stronger to be worth suggesting.
		private const float MinWeaponGain = 1.15f;

		public static int RarityCap()
		{
			if (NPC.downedMoonlord)
				return ItemRarityID.Purple;
			if (NPC.downedAncientCultist)
				return ItemRarityID.Cyan;
			if (NPC.downedPlantBoss)
				return ItemRarityID.Yellow;
			if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3)
				return ItemRarityID.Lime;
			if (NPC.downedMechBossAny)
				return ItemRarityID.Pink;
			if (Main.hardMode)
				return ItemRarityID.LightRed;
			if (NPC.downedBoss3 || NPC.downedQueenBee)
				return ItemRarityID.Orange;
			if (NPC.downedBoss1 || NPC.downedBoss2)
				return ItemRarityID.Green;
			return ItemRarityID.Blue;
		}

		public static HashSet<int> Find(Player player)
		{
			var result = new HashSet<int>();
			List<Item> owned = player.inventory.Concat(player.armor.Take(3)).Where(item => !item.IsAir).ToList();
			var ownedTypes = new HashSet<int>(owned.Select(item => item.type));
			int cap = RarityCap();
			List<Item> candidates = RecipeDatabase.AllItems
				.Where(type => !ownedTypes.Contains(type) && RecipeDatabase.RecipesFor(type).Count > 0)
				.Select(type => ContentSamples.ItemsByType[type])
				.Where(item => item.rare >= ItemRarityID.White && item.rare <= cap)
				.ToList();

			// Weapons, per damage class.
			foreach (var group in owned.Where(IsWeapon).GroupBy(item => item.DamageType.Type)) {
				float best = group.Max(item => Dps(item));
				result.UnionWith(candidates.Where(item => IsWeapon(item) && item.DamageType.Type == group.Key && Dps(item) > best * MinWeaponGain)
					.OrderByDescending(Dps).Take(WeaponsPerClass).Select(item => item.type));
			}

			// Armor, per slot (head, body, legs), against what is equipped there.
			Func<Item, bool>[] slots = { item => item.headSlot >= 0, item => item.bodySlot >= 0, item => item.legSlot >= 0 };
			for (int slot = 0; slot < slots.Length; slot++) {
				Item worn = player.armor[slot];
				int defense = worn.IsAir ? 0 : worn.defense;
				result.UnionWith(candidates.Where(item => slots[slot](item) && !item.vanity && item.defense > defense)
					.OrderByDescending(item => item.defense).ThenBy(item => item.rare).Take(ArmorPerSlot).Select(item => item.type));
			}

			// The strongest pickaxe, if it beats the best one carried.
			int pick = owned.Select(item => item.pick).DefaultIfEmpty(0).Max();
			Item pickaxe = candidates.Where(item => item.pick > pick).OrderByDescending(item => item.pick).FirstOrDefault();
			if (pickaxe != null)
				result.Add(pickaxe.type);
			return result;
		}

		private static bool IsWeapon(Item item) =>
			item.damage > 0 && item.useStyle != ItemUseStyleID.None && item.ammo == AmmoID.None && !item.consumable && !item.accessory
			&& item.pick == 0 && item.axe == 0 && item.hammer == 0 && item.createTile < 0;

		// Damage per second, roughly: damage times uses per second.
		private static float Dps(Item item) => item.damage * 60f / Math.Max(1, item.useTime);
	}
}
