using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Personalities;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TerraIndex.Common
{
	// What each town NPC loves, likes, dislikes and hates (biomes and neighbours), read from the game's personality
	// database, so modded NPCs and their mods' changes to vanilla ones show up too. Shown in the blueprint catalog.
	public static class Happiness
	{
		public static readonly AffectionLevel[] Levels = { AffectionLevel.Love, AffectionLevel.Like, AffectionLevel.Dislike, AffectionLevel.Hate };

		private static List<int> npcs;

		// The database is private in ShopHelper; read once, by reflection (Public too, in case tModLoader exposes it).
		private static PersonalityDatabase Database =>
			typeof(ShopHelper).GetField("_database", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(Main.ShopHelper) as PersonalityDatabase;

		public static void Unload() => npcs = null;

		// Town NPCs with a head icon and some preference, vanilla first.
		public static List<int> Npcs {
			get {
				if (npcs != null)
					return npcs;
				PersonalityDatabase database = Database;
				npcs = new List<int>();
				if (database == null)
					return npcs;
				for (int type = 1; type < NPCLoader.NPCCount; type++) {
					if (ContentSamples.NpcsByNetId.TryGetValue(type, out NPC npc) && npc.townNPC && NPC.TypeToDefaultHeadIndex(type) >= 0
						&& database.GetByNPCID(type)?.ShopModifiers.Count > 0)
						npcs.Add(type);
				}
				return npcs;
			}
		}

		// The names of the biomes and NPCs at that level, biomes first.
		public static List<string> Names(int npcType, AffectionLevel level)
		{
			var names = new List<string>();
			PersonalityProfile profile = Database?.GetByNPCID(npcType);
			if (profile == null)
				return names;
			foreach (IShopPersonalityTrait trait in profile.ShopModifiers) {
				if (trait is BiomePreferenceListTrait biomes)
					names.AddRange(biomes.Where(preference => preference.Affection == level).Select(preference => BiomeName(preference.Biome)));
			}
			foreach (IShopPersonalityTrait trait in profile.ShopModifiers) {
				if (trait is NPCPreferenceTrait neighbour && neighbour.Level == level)
					names.Add(Lang.GetNPCNameValue(neighbour.NpcId));
			}
			return names.Distinct().ToList();
		}

		// Vanilla biomes are named by the game's happiness texts; modded biomes by their display name.
		private static string BiomeName(IShoppingBiome biome)
		{
			if (biome is ModBiome modBiome)
				return modBiome.DisplayName.Value;
			string key = "TownNPCMoodBiomes." + biome.NameKey;
			return Language.Exists(key) ? Language.GetTextValue(key) : biome.NameKey;
		}
	}
}
