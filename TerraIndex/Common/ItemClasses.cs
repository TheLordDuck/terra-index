using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TerraIndex.UI;
using Terraria;
using Terraria.ModLoader;

namespace TerraIndex.Common
{
	// The combat classes the recipe index can filter by: melee, ranged, magic, summon, throwing and the ones mods add
	// (Calamity's rogue, Thorium's bard and healer...). An item's class is its damage type; variants count as their
	// vanilla class (whips are summon, Calamity's true melee is melee). Items without damage, or with class-less damage,
	// have none.
	public static class ItemClasses
	{
		private static readonly DamageClass[] Vanilla = { DamageClass.Melee, DamageClass.Ranged, DamageClass.Magic, DamageClass.Summon };

		private static readonly Dictionary<int, DamageClass> cache = new();
		private static List<DamageClass> all;

		public static void ClearCache()
		{
			cache.Clear();
			all = null;
		}

		public static DamageClass Of(int type)
		{
			if (!cache.TryGetValue(type, out DamageClass damageClass))
				cache[type] = damageClass = Classify(ContentSamples.ItemsByType[type]);
			return damageClass;
		}

		private static DamageClass Classify(Item item)
		{
			DamageClass damageClass = item.DamageType;
			if (item.damage <= 0 || damageClass == null || damageClass == DamageClass.Default || damageClass == DamageClass.Generic)
				return null;
			if (damageClass == DamageClass.SummonMeleeSpeed)
				return DamageClass.Summon;
			if (damageClass == DamageClass.MeleeNoSpeed)
				return DamageClass.Melee;
			if (damageClass == DamageClass.MagicSummonHybrid)
				return DamageClass.Magic;
			return Vanilla.FirstOrDefault(vanilla => damageClass == vanilla || damageClass.CountsAsClass(vanilla)) ?? damageClass;
		}

		// The classes some item in the game has: the vanilla ones first, then throwing, then the modded ones by name.
		public static List<DamageClass> All => all ??= RecipeDatabase.AllItems.Select(Of).Where(damageClass => damageClass != null).Distinct()
			.OrderBy(Order).ThenBy(Name).ToList();

		private static int Order(DamageClass damageClass)
		{
			int index = System.Array.IndexOf(Vanilla, damageClass);
			return index >= 0 ? index : damageClass == DamageClass.Throwing ? Vanilla.Length : Vanilla.Length + 1;
		}

		public static string Name(DamageClass damageClass)
		{
			if (damageClass == DamageClass.Melee)
				return UIHelpers.T("ClassMelee");
			if (damageClass == DamageClass.Ranged)
				return UIHelpers.T("ClassRanged");
			if (damageClass == DamageClass.Magic)
				return UIHelpers.T("ClassMagic");
			if (damageClass == DamageClass.Summon)
				return UIHelpers.T("ClassSummon");
			if (damageClass == DamageClass.Throwing)
				return UIHelpers.T("ClassThrowing");
			if (damageClass is ModDamageClass modded && modded.Mod.Name == "CalamityMod" && modded.Name == "RogueDamageClass")
				return UIHelpers.T("ClassRogue");

			// Other mods: the class's own name ("bard damage" -> "Bard") and its mod.
			string name = damageClass.DisplayName.Value.Trim();
			if (name.EndsWith(" damage"))
				name = name[..^" damage".Length];
			if (name.Length > 0)
				name = char.ToUpper(name[0]) + name[1..];
			if (damageClass is ModDamageClass other)
				name += $" ({Regex.Replace(other.Mod.DisplayName, @"\[c/[0-9a-fA-F]+:([^\]]*)\]", "$1")})";
			return name;
		}
	}
}
