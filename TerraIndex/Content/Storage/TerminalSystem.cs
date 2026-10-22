using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
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
	public class TerminalSystem : ModSystem
	{
		private const int HighlightTicks = 60 * 10;
		private const float MaxDistance = 16 * 10;
		private const float ArrowMargin = 40;

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
		private const int FlyTicks = 26;
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
		}

		// Latest scan (received from the server in multiplayer, computed locally in single player).
		private StorageSnapshot snapshot = new();

		public Point Terminal { get; private set; }
		public int ChestCount => snapshot.ChestCount;
		public List<Item> StoredItems { get; } = new();

		// Bumped whenever new data arrives, so the panel knows when to rebuild its list.
		public int Version { get; private set; }

		// True while either panel is open: the terminal's search or its deposit slots.
		public bool IsOpen => ui?.CurrentState != null;
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

		public override void OnWorldUnload()
		{
			ui?.SetState(null);
			DepositItems = null;
			highlights.Clear();
			doors.Clear();
			drops.Clear();
			flyingItems.Clear();
		}

		public void Open(Point terminal)
		{
			Terminal = terminal;
			Apply(new StorageSnapshot());
			ui.SetState(State);
			SoundEngine.PlaySound(SoundID.MenuOpen);
			RequestRefresh();
		}

		public void Close()
		{
			if (DepositOpen)
				FinishDeposit();
			ui.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public void RequestRefresh()
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
				Apply(StorageNetwork.Scan(Terminal));
			else
				TerminalNet.SendRequest(Terminal);
		}

		// Server reply; ignored if the panel was closed or another terminal opened in the meantime.
		public void ApplyRemote(Point terminal, StorageSnapshot remote)
		{
			if (IsOpen && terminal == Terminal)
				Apply(remote);
		}

		private void Apply(StorageSnapshot newSnapshot)
		{
			snapshot = newSnapshot;
			StoredItems.Clear();
			foreach (var (type, stack) in snapshot.Totals.OrderBy(pair => Lang.GetItemNameValue(pair.Key)))
				StoredItems.Add(new Item(type, stack));
			Version++;
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
			ui.SetState(Deposit);
			// The player's inventory opens next to the slots, like with a chest.
			Main.playerInventory = true;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void ShowDepositDenied() => Main.NewText(UIHelpers.T("DepositInUse"), Color.Orange);

		public void ShowDepositResult(int left, bool moved)
		{
			if (left > 0) {
				Main.NewText(UIHelpers.T("DepositNoSpace", left), Color.Orange);
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
			ShowItemsEntering(Terminal, Main.LocalPlayer.Center, items.Where(item => !item.IsAir).Select(item => item.type).ToList());
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
					TerminalNet.SendItemsEntering(terminal, player, entity.Items.Where(item => !item.IsAir).Select(item => item.type).ToList());
				Flush(entity, player);
			}
		}

		// Client: the items fly one after another from the player into the door, which opens to take them in.
		public void ShowItemsEntering(Point terminal, Vector2 from, List<int> types)
		{
			if (Main.dedServ || types.Count == 0)
				return;
			int stagger = Math.Clamp(40 / types.Count, 1, 4);
			for (int i = 0; i < types.Count; i++)
				flyingItems.Add(new FlyingItem { Type = types[i], From = from, Terminal = terminal, Delay = i * stagger });
			StartDoor(terminal);
		}

		private static Vector2 DoorCenter(Point terminal) =>
			new(terminal.X * 16 + TerminalTile.DoorArea.Center.X, terminal.Y * 16 + TerminalTile.DoorArea.Center.Y);

		// Single player or server: tries to move the terminal's pending items into its chests.
		// player = who to tell the result to, or -1 for the silent automatic retries.
		public void Flush(TerminalEntity entity, int player)
		{
			bool moved = StorageNetwork.Deposit(entity.TopLeft, entity.Items);
			if (moved) {
				if (Main.netMode == NetmodeID.Server)
					TerminalNet.SendDoorOpen(entity.TopLeft);
				else
					StartDoor(entity.TopLeft);
			}

			if (player < 0)
				return;
			int left = entity.Items.Where(item => !item.IsAir).Sum(item => item.stack);
			if (Main.netMode == NetmodeID.Server)
				TerminalNet.SendDepositResult(player, left, moved);
			else
				ShowDepositResult(left, moved);
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
			if (item == null)
				return;

			drops.Add(new PendingDrop { Terminal = terminal, Item = item, Player = player.whoAmI, Ticks = DoorOpenTicks });
			if (Main.netMode == NetmodeID.Server)
				TerminalNet.SendDoorOpen(terminal);
			else
				StartDoor(terminal);
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

			int index = Item.NewItem(source, new Rectangle((int)position.X - 8, (int)position.Y - 8, 16, 16), drop.Item, noBroadcast: true);
			// A small push towards the player, as if the terminal handed it over.
			Main.item[index].velocity = new Vector2(Math.Sign(player.Center.X - position.X) * 2f, -2f);
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
			if (!TerminalTile.IsTerminal(Terminal) || Main.LocalPlayer.Distance(TerminalTile.Center(Terminal)) > MaxDistance || escape) {
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
				if (--drops[i].Ticks <= 0) {
					DropItem(drops[i]);
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
