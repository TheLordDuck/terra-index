using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using TerraIndex.Common;
using TerraIndex.Content.Items;
using TerraIndex.Content.Tiles;
using TerraIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.Content.Storage
{
	// Order of the items in the terminal panel. Ties are broken by name.
	public enum TerminalSort
	{
		Name,
		Amount,
		Rarity,
		Mod,
	}

	public class TerminalSystem : ModSystem
	{
		private const float ArrowMargin = 40;
		// How long highlighted chests glow, and how far the player can walk away before the panel closes (both in ServerConfig/ClientConfig).
		private static int HighlightTicks => 60 * ClientConfig.Instance.HighlightSeconds;
		private static float MaxDistance => 16 * ServerConfig.Instance.TerminalReach;
		// Used at the terminal, it throws withdrawn items out of its door to the player and deposited items fly into it.
		// Used through a portable terminal (even nearby), a drone brings withdrawn items and takes deposited ones.
		// Server: who is using a terminal through a portable terminal, sent with every contents request.
		private static readonly bool[] usingRemote = new bool[Main.maxPlayers];

		public static void SetRemote(int player, bool remote) => usingRemote[player] = remote;

		// Single player or server.
		private bool ByRemote(Player player) => Main.netMode == NetmodeID.SinglePlayer ? Remote : usingRemote[player.whoAmI];

		// Garage door: rolls up, stays open while the item drops out, then rolls back down.
		private const int DoorOpenTicks = 14;
		private const int DoorHoldTicks = 50;
		private const int DoorCloseTicks = 14;

		private UserInterface ui;
		// Panels are built on first use: in Load the translations aren't loaded yet, so the texts would show their keys.
		private TerminalState state;
		private DepositState depositState;
		private TerminalState State => state ??= UIHelpers.Build(new TerminalState());
		private DepositState Deposit => depositState ??= UIHelpers.Build(new DepositState());
		private int sentDepositHash;
		private GameTime lastGameTime;
		private Asset<Texture2D> arrowTexture;
		private Asset<Texture2D> doorTexture;

		// Highlighted chests: chest top-left corner -> ticks remaining.
		private readonly Dictionary<Point, int> highlights = new();

		// Animating doors: terminal top-left corner -> ticks since the door started opening. Client only.
		private readonly Dictionary<Point, int> doors = new();

		// Withdrawn items waiting for the door to open before dropping. Single player or server only.
		private readonly List<PendingDrop> drops = new();

		// Deposited items flying from the player into the terminal's door. Client only, just for show.
		public const int FlyTicks = 26;
		private readonly List<FlyingItem> flyingItems = new();

		private class FlyingItem
		{
			public int Type;
			public Vector2 From;
			public Point Terminal;
			public int Delay;
			public int Age;
		}

		private class PendingDrop
		{
			public Point Terminal;
			public Item Item;
			public int Player;
			public int Ticks;
			// Brought by a drone and dropped above the player, instead of out of the door.
			public bool ByDrone;
		}

		// Latest scan (received from the server in multiplayer, computed locally in single player).
		private StorageSnapshot snapshot = new();

		// The terminal opened last. It stays the crafting terminal after the panel closes (see CanCraft).
		public Point Terminal { get; private set; }
		public bool HasTerminal { get; private set; }
		// Opened with a portable terminal: there is no distance limit while the player carries it.
		public bool Remote { get; private set; }

		// Client: whether the player can still use Terminal. Opened remotely, while carrying the linked portable terminal.
		// In multiplayer the terminal's tiles may not be loaded on this client, so in remote mode the server checks it exists.
		// Worked out once per update: the recipe view asks for every ingredient and station it draws.
		public bool InReach {
			get {
				if (inReachTick != Main.GameUpdateCount) {
					inReachTick = Main.GameUpdateCount;
					inReach = HasTerminal && ComputeInReach();
				}
				return inReach;
			}
		}

		private bool inReach;
		private uint inReachTick = uint.MaxValue;

		private bool ComputeInReach()
		{
			bool exists = (Remote && Main.netMode == NetmodeID.MultiplayerClient) || TerminalTile.IsTerminal(Terminal);
			bool near = Remote ? RemoteTerminalItem.IsLinkedTo(Main.LocalPlayer, Terminal) : Main.LocalPlayer.Distance(TerminalTile.Center(Terminal)) <= MaxDistance;
			return exists && near;
		}

		// Stations the network's wire touches, expanded with what each counts as (a Hellforge is also a Furnace).
		private readonly HashSet<int> networkStations = new();
		// The station tiles themselves (not what they count as), by name, for the terminal panel's stations row.
		public List<int> WireStations { get; } = new();
		// One Item per stored type, reused between refreshes (see Apply).
		private readonly Dictionary<int, Item> itemCache = new();
		private int craftRefreshTimer;
		public TerminalSort Sort { get; private set; }
		public int ChestCount => snapshot.ChestCount;
		public int Slots => snapshot.Slots;
		public int UsedSlots => snapshot.UsedSlots;
		public List<Item> StoredItems { get; } = new();

		// Bumped whenever new data arrives, so the panel knows when to rebuild its list.
		public int Version { get; private set; }

		// True while either panel is open: the terminal's search or its deposit slots.
		public bool IsOpen => ui?.CurrentState != null;
		// Only the search panel, which the recipe index docks next to.
		public bool PanelOpen => state != null && ui.CurrentState == state;
		private static RecipeIndexSystem Index => ModContent.GetInstance<RecipeIndexSystem>();
		public bool DepositOpen => depositState != null && ui.CurrentState == depositState;

		// Contents of the deposit slots being edited. In single player this is the terminal entity's own array;
		// in multiplayer it is a copy that is sent to the server whenever it changes.
		public Item[] DepositItems { get; private set; }

		public override void Load()
		{
			if (!Main.dedServ) {
				ui = new UserInterface();
				LanguageManager.Instance.OnLanguageChanged += RebuildPanels;
				arrowTexture = ModContent.Request<Texture2D>("TerraIndex/Content/Storage/ChestArrow");
				doorTexture = ModContent.Request<Texture2D>("TerraIndex/Content/Tiles/TerminalDoor");
			}
		}

		public override void Unload()
		{
			if (!Main.dedServ)
				LanguageManager.Instance.OnLanguageChanged -= RebuildPanels;
		}

		// Changing the game's language rebuilds the panels (when they are closed) so they pick up the new texts.
		private void RebuildPanels(LanguageManager language)
		{
			if (ui?.CurrentState == null) {
				state = null;
				depositState = null;
			}
		}

		public override void OnWorldLoad() => Sort = ClientConfig.Instance.DefaultSort;

		public override void OnWorldUnload()
		{
			ui?.SetState(null);
			DepositItems = null;
			HasTerminal = false;
			Apply(new StorageSnapshot());
			itemCache.Clear();
			StorageNetwork.ClearCache();
			highlights.Clear();
			doors.Clear();
			drops.Clear();
			flyingItems.Clear();
		}

		public void Open(Point terminal, bool remote)
		{
			Terminal = terminal;
			Remote = remote;
			HasTerminal = true;
			inReachTick = uint.MaxValue;
			Apply(new StorageSnapshot());
			ui.SetState(State);
			Index.OpenWithTerminal();
			SoundEngine.PlaySound(SoundID.MenuOpen);
			RequestRefresh(true);

			TerminalPlayer tips = Main.LocalPlayer.GetModPlayer<TerminalPlayer>();
			if (!tips.SeenOpenTip) {
				tips.SeenOpenTip = true;
				Main.NewText(UIHelpers.T("TipOpened"), Color.LightSkyBlue);
			}
		}

		public void Close()
		{
			if (DepositOpen)
				FinishDeposit();
			Index.CloseWithTerminal();
			ui.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		// The server stays silent when the contents didn't change since its last reply, which saves a lot of traffic.
		// After opening a terminal (contents cleared) the reply is forced, and keeps being asked for until one arrives,
		// in case a request is dropped by the server's rate limit.
		public void RequestRefresh(bool force = false)
		{
			if (force)
				awaitingFullReply = true;
			if (Main.netMode == NetmodeID.SinglePlayer)
				Apply(StorageNetwork.Scan(Terminal));
			else
				TerminalNet.SendRequest(Terminal, awaitingFullReply, Remote);
		}

		private bool awaitingFullReply;

		// Server reply; ignored if another terminal was opened in the meantime. Kept after the panel closes, because
		// the recipe index uses it to craft from the chests.
		public void ApplyRemote(Point terminal, StorageSnapshot remote)
		{
			if (!HasTerminal || terminal != Terminal)
				return;
			awaitingFullReply = false;
			Apply(remote);
		}

		// The list (and Version, which makes the panel rebuild its grid) only changes when the contents do: refreshes
		// arrive twice a second and are usually identical. Item instances are reused, since creating one runs SetDefaults.
		private void Apply(StorageSnapshot newSnapshot)
		{
			bool sameItems = newSnapshot.ChestCount == snapshot.ChestCount && newSnapshot.UsedSlots == snapshot.UsedSlots
				&& newSnapshot.Slots == snapshot.Slots && SameTotals(newSnapshot.Totals, snapshot.Totals);
			bool sameStations = newSnapshot.Stations.SetEquals(snapshot.Stations);
			snapshot = newSnapshot;

			if (!sameStations) {
				WireStations.Clear();
				WireStations.AddRange(snapshot.Stations.OrderBy(RecipeDatabase.TileName));
				networkStations.Clear();
				foreach (int station in snapshot.Stations)
					networkStations.UnionWith(RecipeDatabase.Provides(station));
			}
			if (!sameItems)
				RebuildItems();
		}

		private static bool SameTotals(Dictionary<int, int> a, Dictionary<int, int> b)
		{
			if (a.Count != b.Count)
				return false;
			foreach (var (type, stack) in a) {
				if (!b.TryGetValue(type, out int other) || other != stack)
					return false;
			}
			return true;
		}

		private void RebuildItems()
		{
			StoredItems.Clear();
			foreach (var (type, stack) in snapshot.Totals) {
				if (!itemCache.TryGetValue(type, out Item item))
					itemCache[type] = item = new Item(type);
				item.stack = stack;
				StoredItems.Add(item);
			}
			StoredItems.Sort(CompareItems);
			Version++;
		}

		public void CycleSort()
		{
			Sort = (TerminalSort)(((int)Sort + 1) % Enum.GetValues<TerminalSort>().Length);
			RebuildItems();
		}

		// Amount and rarity go from most to least; mods alphabetically, with vanilla items first.
		private int CompareItems(Item a, Item b)
		{
			int result = Sort switch {
				TerminalSort.Amount => b.stack.CompareTo(a.stack),
				TerminalSort.Rarity => b.rare.CompareTo(a.rare),
				TerminalSort.Mod => string.Compare(a.ModItem?.Mod.DisplayName ?? "", b.ModItem?.Mod.DisplayName ?? "", StringComparison.CurrentCulture),
				_ => 0,
			};
			return result != 0 ? result : string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
		}

		// Highlights the connected chests that hold the item. Returns how many there are.
		public int Highlight(int type)
		{
			highlights.Clear();
			if (snapshot.Locations.TryGetValue(type, out List<Point> chests)) {
				foreach (Point chest in chests)
					highlights[chest] = HighlightTicks;
			}
			return highlights.Count;
		}

		// "Deposit items" button: asks for the terminal's slots (the server, in multiplayer, so only one player uses them).
		public void OpenDeposit()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				TerminalNet.SendDepositRequest(Terminal);
				return;
			}
			Item[] items = BeginDeposit(Terminal, Main.myPlayer);
			if (items == null)
				ShowDepositDenied();
			else
				ShowDeposit(Terminal, items);
		}

		public void ShowDeposit(Point terminal, Item[] items)
		{
			Terminal = terminal;
			DepositItems = items;
			sentDepositHash = DepositHash(items);
			// The inventory takes the index's place.
			Index.CloseWithTerminal();
			ui.SetState(Deposit);
			// The player's inventory opens next to the slots, like with a chest.
			Main.playerInventory = true;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void ShowDepositDenied() => Main.NewText(UIHelpers.T("DepositInUse"), Color.Orange);

		public void ShowDepositResult(int left, bool moved, bool returned)
		{
			if (left > 0) {
				Main.NewText(UIHelpers.T(returned ? "QuickDepositNoSpace" : "DepositNoSpace", left), Color.Orange);
				SoundEngine.PlaySound(SoundID.Unlock);
			}
			else if (moved) {
				Main.NewText(UIHelpers.T("DepositDone"), Color.LightGreen);
			}
		}

		// Closing the deposit slots sends their contents to the chests.
		private void FinishDeposit()
		{
			Item[] items = DepositItems;
			DepositItems = null;
			if (items == null)
				return;
			ShowItemsEntering(Terminal, Main.LocalPlayer, items.Where(item => !item.IsAir).Select(item => item.type).ToList(), Remote);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TerminalNet.SendDepositItems(Terminal, items, true);
			else
				ReceiveDeposit(Terminal, Main.myPlayer, null, true);
		}

		private static int DepositHash(Item[] items)
		{
			var hash = new HashCode();
			foreach (Item item in items)
				hash.Add(item.IsAir ? 0 : (item.type, item.stack, item.prefix).GetHashCode());
			return hash.ToHashCode();
		}

		// Single player or server: locks the terminal's slots for this player. Null if someone else is using them.
		public Item[] BeginDeposit(Point terminal, int player)
		{
			TerminalEntity entity = TerminalEntity.Get(terminal, true);
			if (entity == null)
				return null;
			if (entity.User >= 0 && entity.User != player && Main.player[entity.User].active)
				return null;
			entity.User = player;
			return entity.Items;
		}

		// Single player or server: new contents for the slots (null = already up to date). On close, they go to the chests.
		public void ReceiveDeposit(Point terminal, int player, Item[] items, bool close)
		{
			TerminalEntity entity = TerminalEntity.Get(terminal, false);
			if (entity == null || entity.User != player) {
				// The terminal was broken (or the lock lost) meanwhile: hand the items back instead of losing them.
				if (close && items != null) {
					foreach (Item item in items) {
						if (!item.IsAir)
							Main.player[player].QuickSpawnItem(new EntitySource_Misc("TerraIndex/Deposit"), item, item.stack);
					}
				}
				return;
			}

			if (items != null) {
				for (int i = 0; i < TerminalEntity.Slots; i++)
					entity.Items[i] = i < items.Length ? items[i] : new Item();
			}
			if (close) {
				entity.User = -1;
				// The depositing player already shows the animation; everyone else is told here.
				if (Main.netMode == NetmodeID.Server)
					TerminalNet.SendItemsEntering(terminal, player, entity.Items.Where(item => !item.IsAir).Select(item => item.type).ToList(), usingRemote[player]);
				Flush(entity, player);
			}
		}

		// "Deposit all" button: the main inventory (not coins, ammo or favorites, and not the hotbar unless the client config
		// says so) goes straight into the chests.
		public void QuickDeposit()
		{
			Player player = Main.LocalPlayer;
			var items = new List<Item>();
			for (int slot = ClientConfig.Instance.QuickDepositHotbar ? 0 : 10; slot < 50; slot++) {
				Item item = player.inventory[slot];
				if (item.IsAir || item.favorited || item.IsACoin)
					continue;
				items.Add(item.Clone());
				item.TurnToAir();
				if (Main.netMode == NetmodeID.MultiplayerClient)
					NetMessage.SendData(MessageID.SyncEquipment, number: Main.myPlayer, number2: slot);
			}
			if (items.Count == 0) {
				Main.NewText(UIHelpers.T("QuickDepositEmpty"), Color.Orange);
				return;
			}

			ShowItemsEntering(Terminal, player, items.Select(item => item.type).ToList(), Remote);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TerminalNet.SendQuickDeposit(Terminal, items.ToArray());
			else
				ReceiveQuickDeposit(Terminal, Main.myPlayer, items.ToArray(), true);
		}

		// Single player or server: puts the items into the chests. Whatever doesn't fit (or everything, if the
		// request was refused) is given back to the player.
		public void ReceiveQuickDeposit(Point terminal, int player, Item[] items, bool allowed)
		{
			bool moved = false;
			if (allowed) {
				if (Main.netMode == NetmodeID.Server)
					TerminalNet.SendItemsEntering(terminal, player, items.Select(item => item.type).ToList(), usingRemote[player]);
				moved = StorageNetwork.Deposit(terminal, items);
				if (moved && !ByRemote(Main.player[player]))
					OpenDoor(terminal);
				ShowMoves();
			}

			int left = 0;
			foreach (Item item in items) {
				if (item.IsAir)
					continue;
				left += item.stack;
				Main.player[player].QuickSpawnItem(new EntitySource_Misc("TerraIndex/Deposit"), item, item.stack);
			}

			if (Main.netMode == NetmodeID.Server)
				TerminalNet.SendDepositResult(player, left, moved, true);
			else
				ShowDepositResult(left, moved, true);
		}

		// Client: the items fly one after another from the player into the door, which opens to take them in. Through a
		// portable terminal, a drone comes to take them instead.
		public void ShowItemsEntering(Point terminal, Player player, List<int> types, bool remote)
		{
			if (Main.dedServ || types.Count == 0)
				return;
			if (remote) {
				ModContent.GetInstance<NetworkEffects>().ShowPickupDrone(player.whoAmI, types);
				return;
			}
			Vector2 from = player.Center;
			int stagger = Math.Clamp(40 / types.Count, 1, 4);
			for (int i = 0; i < types.Count; i++)
				flyingItems.Add(new FlyingItem { Type = types[i], From = from, Terminal = terminal, Delay = i * stagger });
			StartDoor(terminal);
		}

		public static Vector2 DoorCenter(Point terminal) =>
			new(terminal.X * 16 + TerminalTile.DoorArea.Center.X, terminal.Y * 16 + TerminalTile.DoorArea.Center.Y);

		// Single player or server: tries to move the terminal's pending items into its chests.
		// player = who to tell the result to, or -1 for the silent automatic retries.
		public void Flush(TerminalEntity entity, int player)
		{
			bool moved = StorageNetwork.Deposit(entity.TopLeft, entity.Items);
			if (moved && (player < 0 || !ByRemote(Main.player[player])))
				OpenDoor(entity.TopLeft);
			ShowMoves();

			if (player < 0)
				return;
			int left = entity.Items.Where(item => !item.IsAir).Sum(item => item.stack);
			if (Main.netMode == NetmodeID.Server)
				TerminalNet.SendDepositResult(player, left, moved, false);
			else
				ShowDepositResult(left, moved, false);
		}

		// Single player or server: the chest effects for what the last operation took out, for every player.
		private static void ShowMoves()
		{
			if (StorageNetwork.Moves.Count == 0)
				return;
			if (Main.netMode == NetmodeID.Server)
				TerminalNet.SendChestMoves(StorageNetwork.Moves);
			else
				ModContent.GetInstance<NetworkEffects>().Show(StorageNetwork.Moves);
			StorageNetwork.Moves.Clear();
		}

		// Single player or server: the door animation, for every player.
		private void OpenDoor(Point terminal)
		{
			if (Main.netMode == NetmodeID.Server)
				TerminalNet.SendDoorOpen(terminal);
			else
				StartDoor(terminal);
		}

		// Takes a stack of the item (or only amount, when above 0) out of the open terminal's network; the server does it in multiplayer.
		public void RequestWithdraw(int type, int amount)
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
				Dispense(Terminal, type, amount, Main.LocalPlayer);
			else
				TerminalNet.SendWithdraw(Terminal, type, amount);
		}

		// Single player or server: removes the item from the chests and drops it out of the door once it is open.
		public void Dispense(Point terminal, int type, int amount, Player player)
		{
			Item item = StorageNetwork.Withdraw(terminal, type, amount);
			if (item != null)
				Deliver(terminal, item, player);
			ShowMoves();
		}

		// Single player or server: throws an item to the player out of the terminal's door, once it has opened. Through a
		// portable terminal, a drone brings it instead (see NetworkEffects).
		private void Deliver(Point terminal, Item item, Player player)
		{
			if (ByRemote(player)) {
				if (Main.netMode == NetmodeID.Server)
					TerminalNet.SendDrone(player.whoAmI, item.type);
				else
					ModContent.GetInstance<NetworkEffects>().ShowDrone(player.whoAmI, item.type);
				drops.Add(new PendingDrop { Terminal = terminal, Item = item, Player = player.whoAmI, Ticks = NetworkEffects.ReleaseTicks, ByDrone = true });
				return;
			}
			OpenDoor(terminal);
			drops.Add(new PendingDrop { Terminal = terminal, Item = item, Player = player.whoAmI, Ticks = DoorOpenTicks });
		}

		// ---- Crafting from the chests (recipe index) ----
		// The client checks stations and conditions, as vanilla crafting does; the server only takes the ingredients
		// out of the chests (all or nothing), so two players can't spend the same items. What the chests lack comes
		// from the inventory: the client takes it out and sends it along (the inventory is the client's), and the
		// server gives back whatever it doesn't use.

		// Crafts per click are capped so "Max" can't flood the world with dropped items.
		private const int MaxCraftsStackable = 999;
		private const int MaxCraftsSingle = 20;

		public bool CanCraft => HasTerminal && InReach;

		// Station near the player (vanilla), or on the network's wire.
		public bool HasStation(int tile) => Main.LocalPlayer.adjTile[tile] || networkStations.Contains(tile);

		public int StoredCount(int type) => snapshot.Totals.GetValueOrDefault(type);

		// How many of the recipe's ingredient can be found in the chests (any member, for groups).
		public int StoredCount(Recipe recipe, int ingredient)
		{
			RecipeGroup group = RecipeDatabase.GroupFor(recipe, ingredient);
			return group == null ? StoredCount(ingredient) : group.ValidItems.Sum(type => StoredCount(type));
		}

		// Client: how many times the recipe can be crafted with what is in the chests and the inventory (0 if a station
		// or condition is missing).
		public int MaxCrafts(Recipe recipe)
		{
			if (!recipe.requiredTile.All(HasStation) || !recipe.Conditions.All(condition => condition.IsMet()))
				return 0;
			int max = recipe.createItem.maxStack > 1 ? MaxCraftsStackable : MaxCraftsSingle;
			Dictionary<int, int> owned = CraftingPlan.Inventory(Main.LocalPlayer);
			foreach (Item ingredient in recipe.requiredItem) {
				int inInventory = RecipeDatabase.AcceptedItems(recipe, ingredient.type).Sum(type => owned.GetValueOrDefault(type));
				max = Math.Min(max, (StoredCount(recipe, ingredient.type) + inInventory) / ingredient.stack);
			}
			return max;
		}

		// Called every frame while the recipe index is open, so its counts follow the chests and the nearby stations.
		public void KeepFreshForCrafting()
		{
			if (!CanCraft || --craftRefreshTimer > 0)
				return;
			craftRefreshTimer = 30;
			// Vanilla only updates the nearby stations (and water, lava...) while the inventory is open.
			Main.LocalPlayer.AdjTiles();
			// The terminal panel already refreshes the contents itself.
			if (!IsOpen)
				RequestRefresh();
		}

		public void RequestCraft(Recipe recipe, int times)
		{
			craftRefreshTimer = 0;
			List<(int Type, int Amount)> fromInventory = FromInventory(recipe, times);
			if (fromInventory == null) {
				ShowCraftResult(recipe.createItem.type, 0);
				return;
			}
			Item[] provided = TakeFromInventory(Main.LocalPlayer, fromInventory);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TerminalNet.SendCraft(Terminal, recipe.RecipeIndex, times, provided);
			else
				Craft(Terminal, recipe.RecipeIndex, times, Main.LocalPlayer, provided, true);
		}

		// Client: what the inventory has to add to the chests for `times` crafts (the chests are used first, so the
		// items carried are kept when possible), or null if not even both together are enough.
		private List<(int Type, int Amount)> FromInventory(Recipe recipe, int times)
		{
			Dictionary<int, int> owned = CraftingPlan.Inventory(Main.LocalPlayer);
			var stored = new Dictionary<int, int>(snapshot.Totals);
			var taking = new List<(int Type, int Amount)>();
			foreach (Item ingredient in recipe.requiredItem) {
				int needed = ingredient.stack * times;
				List<int> accepted = RecipeDatabase.AcceptedItems(recipe, ingredient.type).ToList();
				foreach (int type in accepted) {
					int use = Math.Min(stored.GetValueOrDefault(type), needed);
					stored[type] = stored.GetValueOrDefault(type) - use;
					needed -= use;
				}
				foreach (int type in accepted) {
					int take = Math.Min(owned.GetValueOrDefault(type), needed);
					if (take <= 0)
						continue;
					owned[type] -= take;
					needed -= take;
					taking.Add((type, take));
				}
				if (needed > 0)
					return null;
			}
			return taking;
		}

		// Takes these amounts out of the inventory and returns what was taken, one item per slot touched (prefixes kept,
		// so whatever isn't used can be given back as it was). The caller has checked that the inventory holds them.
		public static Item[] TakeFromInventory(Player player, List<(int Type, int Amount)> items)
		{
			var taken = new List<Item>();
			foreach (var (type, amount) in items) {
				int left = amount;
				for (int slot = 0; slot < player.inventory.Length && left > 0; slot++) {
					Item item = player.inventory[slot];
					if (item.IsAir || item.type != type)
						continue;
					int take = Math.Min(item.stack, left);
					Item part = item.Clone();
					part.stack = take;
					taken.Add(part);
					item.stack -= take;
					left -= take;
					if (item.stack <= 0)
						item.TurnToAir();
					if (Main.netMode == NetmodeID.MultiplayerClient)
						NetMessage.SendData(MessageID.SyncEquipment, number: Main.myPlayer, number2: slot);
				}
			}
			return taken.ToArray();
		}

		// Single player or server. provided: what the client already took out of its inventory for this craft; whatever
		// isn't used (or everything, if the craft fails or the request was refused) is given back.
		public void Craft(Point terminal, int recipeIndex, int times, Player player, Item[] provided, bool allowed)
		{
			var unused = new Dictionary<int, int>();
			foreach (Item item in provided) {
				if (!item.IsAir)
					unused[item.type] = unused.GetValueOrDefault(item.type) + item.stack;
			}

			Recipe recipe = recipeIndex >= 0 && recipeIndex < Recipe.numRecipes ? Main.recipe[recipeIndex] : null;
			if (!allowed || times <= 0 || recipe == null || recipe.Disabled || recipe.createItem.IsAir) {
				GiveBack(player, provided, unused);
				return;
			}
			times = Math.Min(times, recipe.createItem.maxStack > 1 ? MaxCraftsStackable : MaxCraftsSingle);

			int crafted = 0;
			if (StorageNetwork.TakeIngredients(StorageNetwork.FindConnectedChests(terminal), recipe, times, unused)) {
				// Like vanilla crafting: a copy of the result, with a random prefix for weapons and accessories.
				for (int total = recipe.createItem.stack * times; total > 0;) {
					Item item = recipe.createItem.Clone();
					item.stack = Math.Min(total, item.maxStack);
					item.Prefix(-1);
					total -= item.stack;
					crafted += item.stack;
					Deliver(terminal, item, player);
				}
			}
			GiveBack(player, provided, unused);
			ShowMoves();

			if (Main.netMode == NetmodeID.Server)
				TerminalNet.SendCraftResult(player.whoAmI, recipe.createItem.type, crafted);
			else
				ShowCraftResult(recipe.createItem.type, crafted);
		}

		// Gives back to the player the provided items still listed in unused (type -> amount).
		private static void GiveBack(Player player, Item[] provided, Dictionary<int, int> unused)
		{
			foreach (Item item in provided) {
				int amount = item.IsAir ? 0 : Math.Min(item.stack, unused.GetValueOrDefault(item.type));
				if (amount <= 0)
					continue;
				unused[item.type] -= amount;
				player.QuickSpawnItem(new EntitySource_Misc("TerraIndex/Craft"), item, amount);
			}
		}

		// Client: what has to come out of the chests so the inventory has the ingredients for `times` crafts
		// (only what the chests actually hold). Owned items are counted once, even if two ingredients accept them.
		public List<(int Type, int Amount)> MissingIngredients(Recipe recipe, int times)
		{
			Dictionary<int, int> owned = CraftingPlan.Inventory(Main.LocalPlayer);
			var stored = new Dictionary<int, int>(snapshot.Totals);
			var missing = new List<(int Type, int Amount)>();
			foreach (Item ingredient in recipe.requiredItem) {
				int needed = ingredient.stack * times;
				List<int> accepted = RecipeDatabase.AcceptedItems(recipe, ingredient.type).ToList();
				foreach (int type in accepted) {
					int use = Math.Min(owned.GetValueOrDefault(type), needed);
					owned[type] = owned.GetValueOrDefault(type) - use;
					needed -= use;
				}
				foreach (int type in accepted) {
					int take = Math.Min(stored.GetValueOrDefault(type), needed);
					if (take <= 0)
						continue;
					stored[type] -= take;
					needed -= take;
					missing.Add((type, take));
				}
			}
			return missing;
		}

		public void RequestTakeItems(List<(int Type, int Amount)> items)
		{
			if (items.Count == 0)
				return;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TerminalNet.SendTakeItems(Terminal, items);
			else
				TakeItems(Terminal, items, Main.LocalPlayer);
			SoundEngine.PlaySound(SoundID.Grab);
			craftRefreshTimer = 0;
		}

		// Single player or server: takes each item out of the chests (in stacks) and hands it over through the door.
		public void TakeItems(Point terminal, List<(int Type, int Amount)> items, Player player)
		{
			foreach (var (type, amount) in items) {
				for (int left = amount; left > 0;) {
					Item item = StorageNetwork.Withdraw(terminal, type, left);
					if (item == null || item.IsAir)
						break;
					left -= item.stack;
					Deliver(terminal, item, player);
				}
			}
			ShowMoves();
		}

		public void ShowCraftResult(int type, int amount)
		{
			if (amount > 0) {
				SoundEngine.PlaySound(SoundID.Grab);
				Main.NewText(UIHelpers.T("Crafted", amount, Lang.GetItemNameValue(type)), Color.LightGreen);
			}
			else {
				Main.NewText(UIHelpers.T("CraftFailed"), Color.Orange);
			}
		}

		public void StartDoor(Point terminal)
		{
			if (Main.dedServ)
				return;

			if (doors.TryGetValue(terminal, out int ticks)) {
				// Already moving: keep it open, or reopen from where it is if it was closing.
				if (ticks >= DoorOpenTicks + DoorHoldTicks)
					doors[terminal] = (int)(DoorOpenness(ticks) * DoorOpenTicks);
				else if (ticks >= DoorOpenTicks)
					doors[terminal] = DoorOpenTicks;
				return;
			}

			doors[terminal] = 0;
			SoundEngine.PlaySound(SoundID.DoorOpen, TerminalTile.Center(terminal));
		}

		// 0 = closed, 1 = fully open.
		private static float DoorOpenness(int ticks)
		{
			if (ticks < DoorOpenTicks)
				return ticks / (float)DoorOpenTicks;
			if (ticks < DoorOpenTicks + DoorHoldTicks)
				return 1f;
			return Math.Max(0f, 1f - (ticks - DoorOpenTicks - DoorHoldTicks) / (float)DoorCloseTicks);
		}

		private static void DropItem(PendingDrop drop)
		{
			Rectangle door = TerminalTile.DoorArea;
			var position = new Vector2(drop.Terminal.X * 16 + door.Center.X, drop.Terminal.Y * 16 + door.Bottom - 8);
			Player player = Main.player[drop.Player];
			var source = new EntitySource_TileInteraction(player, drop.Terminal.X, drop.Terminal.Y);

			SpawnReserved(source, position, ThrowVelocity(position, player.Center), drop.Item, player);
		}

		// Thrown in an arc that lands on the target: items fall with 0.1 px/tick² of gravity. Farther targets take longer
		// to reach, and the speed is capped so a far throw falls a little short (the player's grab range does the rest).
		private static Vector2 ThrowVelocity(Vector2 from, Vector2 to)
		{
			const float gravity = 0.1f;
			Vector2 delta = to - from;
			float ticks = Math.Clamp(delta.Length() / 5f, 18f, 40f);
			var velocity = new Vector2(delta.X / ticks * 1.05f, delta.Y / ticks - 0.5f * gravity * ticks);
			velocity.X = Math.Clamp(velocity.X, -10f, 10f);
			velocity.Y = Math.Clamp(velocity.Y, -9f, 4f);
			return velocity;
		}

		// The drone lets the item go above the player and it falls onto them (if the player left meanwhile, it still
		// isn't lost: it lands where they were).
		private static void DropFromDrone(PendingDrop drop)
		{
			Player player = Main.player[drop.Player];
			SpawnReserved(new EntitySource_Misc("TerraIndex/Drone"), NetworkEffects.DropPoint(player), new Vector2(0, 1f), drop.Item, player);
		}

		// The item on the ground, which only this player can pick up for a while (see ReservedItem).
		private static void SpawnReserved(IEntitySource source, Vector2 position, Vector2 velocity, Item item, Player player)
		{
			int index = Item.NewItem(source, new Rectangle((int)position.X - 8, (int)position.Y - 8, 16, 16), item, noBroadcast: true);
			Main.item[index].velocity = velocity;
			ReservedItem.Reserve(Main.item[index], player.whoAmI);
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.SyncItem, number: index, number2: 1f);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			lastGameTime = gameTime;
			if (!IsOpen)
				return;

			// Closes like a chest: when walking away, when the terminal is broken, or on Esc (unless typing).
			bool escape = Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape) && !SearchBox.AnyFocused;
			if (!InReach || escape) {
				Close();
				return;
			}

			// The deposit slots also close with the inventory, or when a chest is opened.
			if (DepositOpen && (!Main.playerInventory || Main.LocalPlayer.chest != -1 || Main.LocalPlayer.dead)) {
				Close();
				return;
			}

			ui.Update(gameTime);

			// Multiplayer: keep the server's copy up to date, so nothing is lost if the game closes with the slots open.
			if (DepositOpen && Main.netMode == NetmodeID.MultiplayerClient) {
				int hash = DepositHash(DepositItems);
				if (hash != sentDepositHash) {
					sentDepositHash = hash;
					TerminalNet.SendDepositItems(Terminal, DepositItems, false);
				}
			}
		}

		public override void PostUpdateEverything()
		{
			for (int i = drops.Count - 1; i >= 0; i--) {
				PendingDrop drop = drops[i];
				if (--drop.Ticks <= 0) {
					if (drop.ByDrone)
						DropFromDrone(drop);
					else
						DropItem(drop);
					drops.RemoveAt(i);
				}
			}

			for (int i = flyingItems.Count - 1; i >= 0; i--) {
				FlyingItem flying = flyingItems[i];
				if (flying.Delay > 0) {
					flying.Delay--;
				}
				else if (++flying.Age >= FlyTicks) {
					flyingItems.RemoveAt(i);
					// Keeps the door open while items are still arriving.
					StartDoor(flying.Terminal);
					SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.4f }, DoorCenter(flying.Terminal));
				}
			}

			foreach (Point terminal in doors.Keys.ToList()) {
				int ticks = ++doors[terminal];
				if (ticks >= DoorOpenTicks + DoorHoldTicks + DoorCloseTicks || !TerminalTile.IsTerminal(terminal))
					doors.Remove(terminal);
				else if (ticks == DoorOpenTicks + DoorHoldTicks)
					SoundEngine.PlaySound(SoundID.DoorClosed, TerminalTile.Center(terminal));
			}

			foreach (Point chest in highlights.Keys.ToList()) {
				if (--highlights[chest] <= 0) {
					highlights.Remove(chest);
					continue;
				}
				Vector2 center = new Vector2(chest.X * 16 + ChestWidth(chest) * 8, chest.Y * 16 + 16);
				Lighting.AddLight(center, 1f, 0.85f, 0.3f);
				if (Main.rand.NextBool(8))
					Dust.NewDust(new Vector2(chest.X * 16, chest.Y * 16), ChestWidth(chest) * 16, 32, DustID.GoldFlame);
			}
		}

		public override void PostDrawTiles()
		{
			if (highlights.Count == 0 && doors.Count == 0 && flyingItems.Count == 0)
				return;

			SpriteBatch spriteBatch = Main.spriteBatch;
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

			// The tile sprite shows the door closed; while it moves, draw the dark inside and the part of the door still down.
			Texture2D doorSprite = doorTexture.Value;
			foreach (var (terminal, ticks) in doors) {
				Rectangle door = TerminalTile.DoorArea;
				var area = new Rectangle(terminal.X * 16 + door.X - (int)Main.screenPosition.X, terminal.Y * 16 + door.Y - (int)Main.screenPosition.Y, door.Width, door.Height);
				Color light = Lighting.GetColor(terminal.X + 3, terminal.Y + 1);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, new Color(10, 12, 16).MultiplyRGB(light));

				int visible = (int)MathF.Round(door.Height * (1f - DoorOpenness(ticks)));
				if (visible > 0)
					spriteBatch.Draw(doorSprite, new Rectangle(area.X, area.Y, door.Width, visible), new Rectangle(0, door.Height - visible, door.Width, visible), light);
			}

			// Flying items: an arc from the player to the door, shrinking as they go in.
			foreach (FlyingItem flying in flyingItems) {
				if (flying.Delay > 0)
					continue;
				float t = flying.Age / (float)FlyTicks;
				Vector2 position = Vector2.Lerp(flying.From, DoorCenter(flying.Terminal), t) - new Vector2(0, MathF.Sin(t * MathF.PI) * 32f);
				Main.instance.LoadItem(flying.Type);
				Main.GetItemDrawFrame(flying.Type, out Texture2D texture, out Rectangle frame);
				float scale = Math.Min(1f, 24f / Math.Max(frame.Width, frame.Height)) * (1f - 0.5f * t);
				Color light = Lighting.GetColor((int)(position.X / 16), (int)(position.Y / 16));
				spriteBatch.Draw(texture, position - Main.screenPosition, frame, light, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
			}

			float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
			foreach (Point chest in highlights.Keys) {
				var area = new Rectangle(chest.X * 16 - (int)Main.screenPosition.X - 2, chest.Y * 16 - (int)Main.screenPosition.Y - 2, ChestWidth(chest) * 16 + 4, 36);
				spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, Color.Gold * (0.15f + 0.15f * pulse));
				DrawBorder(spriteBatch, area, Color.Gold * (0.6f + 0.4f * pulse), 2);
			}

			spriteBatch.End();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index == -1)
				return;

			layers.Insert(index, new LegacyGameInterfaceLayer("TerraIndex: Terminal", () => {
				if (IsOpen && lastGameTime != null)
					ui.Draw(Main.spriteBatch, lastGameTime);
				return true;
			}, InterfaceScaleType.UI));

			layers.Insert(index, new LegacyGameInterfaceLayer("TerraIndex: Chest Arrows", () => {
				DrawOffscreenArrows(Main.spriteBatch);
				return true;
			}, InterfaceScaleType.UI));
		}

		// For each highlighted chest off screen, an arrow on the screen edge pointing at it, with the distance in blocks.
		private void DrawOffscreenArrows(SpriteBatch spriteBatch)
		{
			if (highlights.Count == 0)
				return;

			// The layer uses UI scale, so work in screen pixels and then divide by Main.UIScale.
			var screen = new Vector2(Main.screenWidth, Main.screenHeight);
			Vector2 center = screen / 2f;
			Texture2D texture = arrowTexture.Value;
			float alpha = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);

			foreach (Point chest in highlights.Keys) {
				var world = new Vector2(chest.X * 16 + ChestWidth(chest) * 8, chest.Y * 16 + 16);
				Vector2 onScreen = Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
				if (onScreen.X >= 0 && onScreen.Y >= 0 && onScreen.X <= screen.X && onScreen.Y <= screen.Y)
					continue;

				Vector2 direction = onScreen - center;
				float toEdge = MathF.Min((center.X - ArrowMargin) / MathF.Abs(direction.X), (center.Y - ArrowMargin) / MathF.Abs(direction.Y));
				Vector2 position = (center + direction * toEdge) / Main.UIScale;

				spriteBatch.Draw(texture, position, null, Color.White * alpha, direction.ToRotation(), texture.Size() / 2f, 1.5f, SpriteEffects.None, 0f);

				int blocks = (int)(Vector2.Distance(Main.LocalPlayer.Center, world) / 16);
				string text = UIHelpers.T("Blocks", blocks);
				Vector2 textSize = UIHelpers.Measure(text, 0.8f);
				Vector2 textPosition = position - direction.SafeNormalize(Vector2.Zero) * 26 - textSize / 2f;
				Utils.DrawBorderString(spriteBatch, text, textPosition, Color.Gold, 0.8f);
			}
		}

		// Chests are 2 tiles wide; dressers are 3.
		private static int ChestWidth(Point chest) => TileID.Sets.BasicDresser[Main.tile[chest.X, chest.Y].TileType] ? 3 : 2;

		private static void DrawBorder(SpriteBatch spriteBatch, Rectangle area, Color color, int thickness)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y, area.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(area.X, area.Bottom - thickness, area.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(area.X, area.Y, thickness, area.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(area.Right - thickness, area.Y, thickness, area.Height), color);
		}
	}
}
