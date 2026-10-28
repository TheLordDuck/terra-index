using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TerraIndex.Common
{
	// What each town NPC loves, likes, dislikes and hates (biomes and neighbours), read from the game's personality
	// database, so modded NPCs and their mods' changes to vanilla ones show up too. Shown in the blueprint catalog.
	public static class Happiness
	{
		public static readonly AffectionLevel[] Levels = { AffectionLevel.Love, AffectionLevel.Like, AffectionLevel.Dislike, AffectionLevel.Hate };

		// Frames of the bestiary's biome icons (Images/UI/Bestiary/Icon_Tags_Shadow, 16 x 5) for the vanilla shopping biomes.
		private static readonly Dictionary<string, int> BiomeFrames = new() {
			["Forest"] = 0, ["NormalUnderground"] = 1, ["Desert"] = 3, ["Snow"] = 5, ["Corruption"] = 7, ["Crimson"] = 12,
			["Hallow"] = 17, ["Jungle"] = 22, ["Mushroom"] = 25, ["Ocean"] = 28, ["Dungeon"] = 32,
		};

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
						&& database.TryGetProfileByNPCID(type, out PersonalityProfile profile) && profile.ShopModifiers.Count > 0)
						npcs.Add(type);
				}
				return npcs;
			}
		}

		private static List<IShopPersonalityTrait> Traits(int npcType)
		{
			PersonalityDatabase database = Database;
			return database != null && database.TryGetProfileByNPCID(npcType, out PersonalityProfile profile) ? profile.ShopModifiers : new List<IShopPersonalityTrait>();
		}

		// The biomes the NPC feels that way about.
		public static List<IShoppingBiome> Biomes(int npcType, AffectionLevel level) =>
			Traits(npcType).OfType<BiomePreferenceListTrait>().SelectMany(biomes => biomes)
				.Where(preference => preference.Affection == level).Select(preference => preference.Biome).Distinct().ToList();

		// The neighbours the NPC feels that way about.
		public static List<int> Neighbours(int npcType, AffectionLevel level) =>
			Traits(npcType).OfType<NPCPreferenceTrait>().Where(neighbour => neighbour.Level == level).Select(neighbour => neighbour.NpcId).Distinct().ToList();

		// Vanilla biomes are named by the game's happiness texts; modded biomes by their display name.
		public static string BiomeName(IShoppingBiome biome)
		{
			if (biome is ModBiome modBiome)
				return modBiome.DisplayName.Value;
			string key = "TownNPCMoodBiomes." + biome.NameKey;
			return Language.Exists(key) ? Language.GetTextValue(key) : biome.NameKey;
		}

		// The biome's bestiary icon, or false if it has none (it is then shown by name).
		public static bool BiomeIcon(IShoppingBiome biome, out Texture2D texture, out Rectangle frame)
		{
			if (biome is ModBiome modBiome && ModContent.RequestIfExists(modBiome.BestiaryIcon, out Asset<Texture2D> asset, AssetRequestMode.ImmediateLoad)) {
				texture = asset.Value;
				frame = texture.Bounds;
				return true;
			}
			if (biome.NameKey != null && BiomeFrames.TryGetValue(biome.NameKey, out int index)) {
				texture = Main.Assets.Request<Texture2D>("Images/UI/Bestiary/Icon_Tags_Shadow", AssetRequestMode.ImmediateLoad).Value;
				frame = texture.Frame(16, 5, index % 16, index / 16);
				return true;
			}
			texture = null;
			frame = default;
			return false;
		}

		// The NPC's map head, or null if it has none.
		public static Texture2D Head(int npcType)
		{
			int index = NPC.TypeToDefaultHeadIndex(npcType);
			return index >= 0 ? TextureAssets.NpcHead[index].Value : null;
		}
	}
}
