using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TerraIndex.Common;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TerraIndex.Content.Building
{
	// Pieces of a material's set (see BuildMaterial).
	public enum PieceKind
	{
		Block,
		Wall,
		Platform,
		Door,
		Chair,
		Table,
		WorkBench,
		Bed,
		Bookcase,
		Chandelier,
		Lantern,
		Candle,
		Clock,
	}

	// Slopes are named after their solid corner: DownRight is solid at the bottom right (the left side of a roof).
	public enum BrushShape
	{
		Full,
		SlopeDownLeft,
		SlopeDownRight,
		SlopeUpLeft,
		SlopeUpRight,
	}

	// What a blueprint character places: one of the chosen material's pieces, or a fixed item (the first of Items
	// this game has), each one falling back to the other. A material without the piece falls back to plain wood's.
	// No item at all: nothing is placed there.
	public class Brush
	{
		private readonly PieceKind? kind;
		private readonly bool fixedFirst;
		private readonly string[] names;
		private int[] items;

		public readonly BrushShape Shape;

		private Brush(PieceKind? kind, bool fixedFirst, BrushShape shape, string[] names)
		{
			this.kind = kind;
			this.fixedFirst = fixedFirst;
			Shape = shape;
			this.names = names;
		}

		public static Brush Of(PieceKind kind, params string[] fallbacks) => new(kind, false, BrushShape.Full, fallbacks);

		public static Brush Fixed(params string[] names) => new(null, true, BrushShape.Full, names);

		public static Brush Fixed(PieceKind fallback, params string[] names) => new(fallback, true, BrushShape.Full, names);

		public static Brush Sloped(BrushShape shape, params string[] names) => new(null, true, shape, names);

		public int Item(BuildMaterial material)
		{
			// Item names are looked up once (vanilla names, so this works at any time after loading).
			items ??= names.Select(name => ItemID.Search.TryGetId(name, out int id) ? id : 0).Where(id => id > 0).ToArray();
			int piece = kind is PieceKind k ? material?.Piece(k) ?? 0 : 0;
			int fixedItem = items.Length > 0 ? items[0] : 0;
			return fixedFirst ? (fixedItem > 0 ? fixedItem : piece) : (piece > 0 ? piece : fixedItem);
		}
	}

	// A house plan: two layers of text rows of the same size, the tiles and the background walls. Generated from
	// tools/blueprints/designs.py (see BlueprintDesigns), which also checks them and draws them.
	// Tile layer: ' ' leaves the cell as it is, '.' must be empty, '+' is covered by a piece of furniture whose
	// top-left cell has its letter; any other character is a brush (TileBrushes). Wall layer: ' ' leaves the
	// background wall as it is; any other character is a brush (WallBrushes).
	public class Blueprint
	{
		public readonly string Key;
		private readonly string[] tiles;
		private readonly string[] walls;

		public int Width => tiles[0].Length;
		public int Height => tiles.Length;

		public Blueprint(string key, string[] tiles, string[] walls)
		{
			Key = key;
			this.tiles = tiles;
			this.walls = walls;
		}

		public char TileAt(int x, int y) => tiles[y][x];
		public char WallAt(int x, int y) => walls[y][x];

	}

	// A piece of a material's set: the item placed.
	public record Piece(int Item);

	// A material a house can be built of: a block plus the wall, platform, door, chair and table made from it (found
	// in the recipes, so modded woods and bricks work too), and whatever other furniture vanilla makes from it.
	public class BuildMaterial
	{
		public readonly int Block;
		public readonly Dictionary<PieceKind, Piece> Pieces = new();

		public BuildMaterial(int block)
		{
			Block = block;
			Pieces[PieceKind.Block] = new Piece(block);
		}

		public string Name => Lang.GetItemNameValue(Block);

		// Its piece of this kind, or plain wood's when it has none (0 if neither has one).
		public int Piece(PieceKind kind)
		{
			if (Pieces.TryGetValue(kind, out Piece piece))
				return piece.Item;
			return Blueprints.Wood != null && Blueprints.Wood != this && Blueprints.Wood.Pieces.TryGetValue(kind, out piece) ? piece.Item : 0;
		}
	}

	public class Blueprints : ModSystem
	{
		public static Blueprint[] All => BlueprintDesigns.All;

		// Keep in sync with the character sets in tools/blueprints/build.py, which checks and draws the designs.
		public static readonly Dictionary<char, Brush> TileBrushes = new() {
			['#'] = Brush.Of(PieceKind.Block),
			['-'] = Brush.Of(PieceKind.Platform),
			['D'] = Brush.Of(PieceKind.Door),
			['C'] = Brush.Of(PieceKind.Chair),
			['T'] = Brush.Of(PieceKind.Table),
			['W'] = Brush.Of(PieceKind.WorkBench, "WorkBench"),
			['B'] = Brush.Of(PieceKind.Bed, "Bed"),
			['K'] = Brush.Of(PieceKind.Bookcase, "Bookcase"),
			// Lights fall back to a torch, so the rooms always count as housing.
			['L'] = Brush.Of(PieceKind.Chandelier, "CopperChandelier", "TinChandelier", "Torch"),
			['l'] = Brush.Of(PieceKind.Lantern, "ChineseLantern", "Torch"),
			['c'] = Brush.Of(PieceKind.Candle, "Candle", "Torch"),
			['O'] = Brush.Of(PieceKind.Clock, "GrandfatherClock"),
			['y'] = Brush.Fixed(PieceKind.Lantern, "DynastyLantern"),
			['i'] = Brush.Fixed("Torch"),
			['F'] = Brush.Fixed("Fireplace"),
			['n'] = Brush.Fixed("RedBanner"),
			['b'] = Brush.Fixed("Bottle"),
			['|'] = Brush.Fixed("WoodenBeam"),
			['S'] = Brush.Fixed("GrayBrick"),
			['R'] = Brush.Fixed("RedBrick"),
			['/'] = Brush.Sloped(BrushShape.SlopeDownRight, "RedBrick"),
			['\\'] = Brush.Sloped(BrushShape.SlopeDownLeft, "RedBrick"),
			['d'] = Brush.Fixed(PieceKind.Block, "DynastyWood"),
			['Y'] = Brush.Fixed("RedDynastyShingles", "RedBrick"),
			['('] = Brush.Sloped(BrushShape.SlopeDownRight, "RedDynastyShingles", "RedBrick"),
			[')'] = Brush.Sloped(BrushShape.SlopeDownLeft, "RedDynastyShingles", "RedBrick"),
			['U'] = Brush.Fixed("BlueDynastyShingles", "ObsidianBrick"),
			['['] = Brush.Sloped(BrushShape.SlopeDownRight, "BlueDynastyShingles", "ObsidianBrick"),
			[']'] = Brush.Sloped(BrushShape.SlopeDownLeft, "BlueDynastyShingles", "ObsidianBrick"),
			// Stone slab, and its slopes with the solid corner on top (arches).
			['E'] = Brush.Fixed("StoneSlab", "GrayBrick"),
			['<'] = Brush.Sloped(BrushShape.SlopeUpLeft, "StoneSlab", "GrayBrick"),
			['>'] = Brush.Sloped(BrushShape.SlopeUpRight, "StoneSlab", "GrayBrick"),
			// Decorations.
			['f'] = Brush.Fixed("Campfire"),
			['Z'] = Brush.Fixed("WoodenCrate"),
			['P'] = Brush.Fixed("Piano"),
			['G'] = Brush.Fixed("WoodenSink"),
			['j'] = Brush.Fixed("Chain"),
			['h'] = Brush.Fixed("Book"),
			['o'] = Brush.Fixed("Bowl"),
			['u'] = Brush.Fixed("Mug"),
			['Q'] = Brush.Fixed("Throne"),
			['V'] = Brush.Fixed("Sofa"),
			['H'] = Brush.Fixed("Bathtub"),
			['t'] = Brush.Fixed("Toilet"),
			// Trees: the wands place living wood and leaves (building is free, so no wood or acorns are taken).
			['v'] = Brush.Fixed("LivingWoodWand", "Wood"),
			['e'] = Brush.Fixed("LeafWand"),
			['1'] = Brush.Sloped(BrushShape.SlopeDownRight, "LeafWand"),
			['2'] = Brush.Sloped(BrushShape.SlopeDownLeft, "LeafWand"),
			['3'] = Brush.Sloped(BrushShape.SlopeUpLeft, "LeafWand"),
			['4'] = Brush.Sloped(BrushShape.SlopeUpRight, "LeafWand"),
			['5'] = Brush.Sloped(BrushShape.SlopeDownRight, "LivingWoodWand", "Wood"),
			['6'] = Brush.Sloped(BrushShape.SlopeDownLeft, "LivingWoodWand", "Wood"),
			['7'] = Brush.Sloped(BrushShape.SlopeUpLeft, "LivingWoodWand", "Wood"),
			['8'] = Brush.Sloped(BrushShape.SlopeUpRight, "LivingWoodWand", "Wood"),
			['r'] = Brush.Fixed("VineRope", "Rope"),
		};

		public static readonly Dictionary<char, Brush> WallBrushes = new() {
			['w'] = Brush.Of(PieceKind.Wall),
			['s'] = Brush.Fixed("GrayBrickWall"),
			['g'] = Brush.Fixed("GlassWall"),
			['k'] = Brush.Fixed(PieceKind.Wall, "WhiteDynastyWall"),
			['q'] = Brush.Fixed(PieceKind.Wall, "BlueDynastyWall"),
			['p'] = Brush.Fixed(PieceKind.Wall, "PlankedWall"),
			['x'] = Brush.Fixed("StoneSlabWall", "GrayBrickWall"),
			['v'] = Brush.Fixed("LivingWoodWall", "WoodWall"),
			['a'] = Brush.Fixed("LivingLeafWall"),
			['z'] = Brush.Fixed("WoodenFence"),
		};

		private static readonly PieceKind[] Required = { PieceKind.Wall, PieceKind.Platform, PieceKind.Door, PieceKind.Chair, PieceKind.Table };

		// Plain wood first, then by name.
		public static List<BuildMaterial> Materials { get; } = new();
		public static BuildMaterial Wood { get; private set; }

		public override void PostSetupRecipes() => FindMaterials();

		public override void Unload()
		{
			Materials.Clear();
			Wood = null;
		}

		public static BuildMaterial Find(int block) => Materials.Find(material => material.Block == block);

		private static void FindMaterials()
		{
			Materials.Clear();
			var byBlock = new Dictionary<int, BuildMaterial>();
			for (int i = 0; i < Recipe.numRecipes; i++) {
				Recipe recipe = Main.recipe[i];
				if (recipe == null || recipe.Disabled || recipe.createItem.IsAir)
					continue;
				// Made from exactly one kind of block (a chandelier also takes torches and a chain).
				List<Item> blocks = recipe.requiredItem.Where(item => IsBlock(item.type)).ToList();
				if (blocks.Count != 1 || Classify(recipe.createItem) is not PieceKind kind)
					continue;

				int block = blocks[0].type;
				if (!byBlock.TryGetValue(block, out BuildMaterial material))
					byBlock[block] = material = new BuildMaterial(block);
				material.Pieces.TryAdd(kind, new Piece(recipe.createItem.type));
			}

			foreach (BuildMaterial material in byBlock.Values) {
				if (Required.All(material.Pieces.ContainsKey))
					Materials.Add(material);
			}
			Materials.Sort((a, b) => a.Block == ItemID.Wood ? -1 : b.Block == ItemID.Wood ? 1 : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
			Wood = Find(ItemID.Wood);
		}

		// A plain solid block (not furniture, not a platform).
		private static bool IsBlock(int type)
		{
			Item item = ContentSamples.ItemsByType[type];
			int tile = item.createTile;
			return tile >= 0 && Main.tileSolid[tile] && !Main.tileSolidTop[tile] && !Main.tileFrameImportant[tile];
		}

		private static readonly (int Tile, PieceKind Kind, int Width, int Height)[] VanillaFurniture = {
			(TileID.ClosedDoor, PieceKind.Door, 1, 3),
			(TileID.Chairs, PieceKind.Chair, 1, 2),
			(TileID.Tables, PieceKind.Table, 3, 2),
			(TileID.Tables2, PieceKind.Table, 3, 2),
			(TileID.WorkBenches, PieceKind.WorkBench, 2, 1),
			(TileID.Beds, PieceKind.Bed, 4, 2),
			(TileID.Bookcases, PieceKind.Bookcase, 3, 4),
			(TileID.Chandeliers, PieceKind.Chandelier, 3, 3),
			(TileID.HangingLanterns, PieceKind.Lantern, 1, 2),
			(TileID.Candles, PieceKind.Candle, 1, 1),
			(TileID.GrandfatherClocks, PieceKind.Clock, 2, 5),
		};

		// Vanilla furniture by its exact tile and size (so dressers don't pass for tables, or toilets for chairs);
		// modded doors, chairs and tables by what they count as for housing, and their size.
		private static PieceKind? Classify(Item item)
		{
			// Fences are walls too, but they don't count for housing.
			if (item.createWall > 0)
				return Main.wallHouse[item.createWall] ? PieceKind.Wall : null;
			int tile = item.createTile;
			if (tile < 0)
				return null;
			if (TileID.Sets.Platforms[tile])
				return PieceKind.Platform;

			TileObjectData data = TileObjectData.GetTileData(tile, item.placeStyle);
			if (data == null)
				return null;
			if (tile < TileID.Count) {
				foreach (var (vanillaTile, kind, width, height) in VanillaFurniture) {
					if (tile == vanillaTile && data.Width == width && data.Height == height)
						return kind;
				}
				return null;
			}
			if (data.Width == 1 && data.Height == 3 && TileID.Sets.OpenDoorID[tile] >= 0)
				return PieceKind.Door;
			if (data.Width == 1 && data.Height == 2 && TileID.Sets.RoomNeeds.CountsAsChair.Contains(tile))
				return PieceKind.Chair;
			if (data.Width == 3 && data.Height == 2 && TileID.Sets.RoomNeeds.CountsAsTable.Contains(tile))
				return PieceKind.Table;
			return null;
		}

		// Top-left corner of the blueprint when its bottom row sits on this tile, centered on it.
		public static Point Origin(Blueprint blueprint, Point tile) => new(tile.X - blueprint.Width / 2, tile.Y - blueprint.Height + 1);

		private static bool IsSolid(int item)
		{
			int tile = ContentSamples.ItemsByType[item].createTile;
			return tile >= 0 && Main.tileSolid[tile] && !Main.tileSolidTop[tile];
		}

		// Cells where something is in the way: any tile but grass and plants (cleared when building) or the very block
		// that goes there (so a village's trunks can grow out of living wood trees already there), or a player
		// standing where a solid block goes. Empty: it can be built.
		public static List<Point> Blocked(Blueprint blueprint, BuildMaterial material, Point origin)
		{
			var players = new List<Rectangle>();
			foreach (Player player in Main.player) {
				if (player.active && !player.dead)
					players.Add(player.Hitbox);
			}

			var blocked = new List<Point>();
			for (int y = 0; y < blueprint.Height; y++) {
				for (int x = 0; x < blueprint.Width; x++) {
					char cell = blueprint.TileAt(x, y);
					if (cell == ' ')
						continue;
					int i = origin.X + x;
					int j = origin.Y + y;
					if (!WorldGen.InWorld(i, j, 10)) {
						blocked.Add(new Point(i, j));
						continue;
					}
					Tile tile = Main.tile[i, j];
					int item = TileBrushes.TryGetValue(cell, out Brush brush) ? brush.Item(material) : 0;
					bool inTheWay = tile.HasTile && !Main.tileCut[tile.TileType] && !(item > 0 && IsSolid(item) && ContentSamples.ItemsByType[item].createTile == tile.TileType);
					if (!inTheWay && item > 0 && IsSolid(item))
						inTheWay = players.Exists(hitbox => hitbox.Intersects(new Rectangle(i * 16, j * 16, 16, 16)));
					if (inTheWay)
						blocked.Add(new Point(i, j));
				}
			}
			return blocked;
		}

		// Size of what an item places (1x1 for blocks and anything without object data).
		public static Point Size(int item)
		{
			Item sample = ContentSamples.ItemsByType[item];
			TileObjectData data = sample.createTile >= 0 ? TileObjectData.GetTileData(sample.createTile, sample.placeStyle) : null;
			return data == null ? new Point(1, 1) : new Point(data.Width, data.Height);
		}

		// Single player or server: builds it (check Blocked first). Order: background walls, then blocks, platforms and
		// beams (shaped into slopes), then furniture, which needs them to stand on and hang from, then small things
		// like torches and candles, which go on walls and tables.
		public static void Place(Blueprint blueprint, BuildMaterial material, Point origin)
		{
			int width = blueprint.Width;
			int height = blueprint.Height;
			var cells = new List<(int I, int J, Brush Brush, Item Item)>();
			for (int y = 0; y < height; y++) {
				for (int x = 0; x < width; x++) {
					int i = origin.X + x;
					int j = origin.Y + y;
					if (blueprint.TileAt(x, y) == ' ')
						continue;
					Tile tile = Main.tile[i, j];
					if (tile.HasTile && Main.tileCut[tile.TileType])
						WorldGen.KillTile(i, j, noItem: true);
					if (TileBrushes.TryGetValue(blueprint.TileAt(x, y), out Brush brush) && brush.Item(material) is int item and > 0)
						cells.Add((i, j, brush, ContentSamples.ItemsByType[item]));
				}
			}

			for (int y = 0; y < height; y++) {
				for (int x = 0; x < width; x++) {
					int i = origin.X + x;
					int j = origin.Y + y;
					if (WallBrushes.TryGetValue(blueprint.WallAt(x, y), out Brush brush) && brush.Item(material) is int item and > 0
						&& Main.tile[i, j].WallType == 0)
						WorldGen.PlaceWall(i, j, ContentSamples.ItemsByType[item].createWall, mute: true);
				}
			}

			foreach (var (i, j, brush, item) in cells) {
				if (TileObjectData.GetTileData(item.createTile, item.placeStyle) != null && !TileID.Sets.Platforms[item.createTile])
					continue;
				WorldGen.PlaceTile(i, j, item.createTile, mute: true, style: item.placeStyle);
				Tile tile = Main.tile[i, j];
				if (brush.Shape != BrushShape.Full && tile.HasTile) {
					tile.Slope = brush.Shape switch {
						BrushShape.SlopeDownLeft => SlopeType.SlopeDownLeft,
						BrushShape.SlopeDownRight => SlopeType.SlopeDownRight,
						BrushShape.SlopeUpLeft => SlopeType.SlopeUpLeft,
						_ => SlopeType.SlopeUpRight,
					};
				}
			}

			foreach (bool small in new[] { false, true }) {
				foreach (var (i, j, _, item) in cells) {
					TileObjectData data = TileObjectData.GetTileData(item.createTile, item.placeStyle);
					if (data == null || TileID.Sets.Platforms[item.createTile] || (data.Width == 1 && data.Height == 1) != small)
						continue;
					if (small)
						WorldGen.PlaceTile(i, j, item.createTile, mute: true, style: item.placeStyle);
					else if (TileObject.CanPlace(i + data.Origin.X, j + data.Origin.Y, item.createTile, item.placeStyle, 1, out TileObject placed))
						TileObject.Place(placed);
				}
			}

			WorldGen.RangeFrame(origin.X - 1, origin.Y - 1, origin.X + width + 1, origin.Y + height + 1);

			// Tile squares are kept small so each packet stays well under the size limit.
			if (Main.netMode == NetmodeID.Server) {
				const int Chunk = 30;
				for (int x = origin.X - 1; x <= origin.X + width; x += Chunk) {
					for (int y = origin.Y - 1; y <= origin.Y + height; y += Chunk)
						NetMessage.SendTileSquare(-1, x, y, Math.Min(Chunk, origin.X + width + 1 - x), Math.Min(Chunk, origin.Y + height + 1 - y));
				}
			}
		}
	}
}
