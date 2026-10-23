using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TerraIndex.Content.Storage;
using TerraIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex
{
	public class RecipeIndexSystem : ModSystem
	{
		public static ModKeybind ToggleKey { get; private set; }
		public static ModKeybind RecipesKey { get; private set; }
		public static ModKeybind UsesKey { get; private set; }

		// Item under the mouse last frame (inventory, chests, our grid...).
		public static int HoveredItem { get; private set; }

		private UserInterface ui;
		// Built on first use: in Load the translations aren't loaded yet, so the texts would show their keys.
		private BrowserState state;
		private BrowserState State => state ??= UIHelpers.Build(new BrowserState());
		private GameTime lastGameTime;

		// Opened while the terminal panel is open, the index sits next to it (BrowserState.Layout) and then opens and
		// closes along with it. Closing the index by hand while the terminal is open stops that, until it is opened
		// next to the terminal again.
		private bool withTerminal;
		private static TerminalSystem Terminal => ModContent.GetInstance<TerminalSystem>();

		public bool IsOpen => ui?.CurrentState != null;

		public override void Load()
		{
			ToggleKey = KeybindLoader.RegisterKeybind(Mod, "ToggleBrowser", Keys.N);
			RecipesKey = KeybindLoader.RegisterKeybind(Mod, "ShowRecipes", Keys.OemOpenBrackets);
			UsesKey = KeybindLoader.RegisterKeybind(Mod, "ShowUses", Keys.OemCloseBrackets);

			if (!Main.dedServ) {
				ui = new UserInterface();
				LanguageManager.Instance.OnLanguageChanged += RebuildPanel;
			}
		}

		public override void Unload()
		{
			ToggleKey = RecipesKey = UsesKey = null;
			ItemGrid.ClearCache();
			if (!Main.dedServ)
				LanguageManager.Instance.OnLanguageChanged -= RebuildPanel;
		}

		// Changing the game's language rebuilds the panel (when it is closed) so it picks up the new texts.
		private void RebuildPanel(LanguageManager language)
		{
			if (ui?.CurrentState == null)
				state = null;
		}

		public override void OnWorldUnload() => ui?.SetState(null);

		public void Toggle()
		{
			if (ui.CurrentState == null)
				Open();
			else
				Close();
		}

		public void Open()
		{
			if (Terminal.PanelOpen)
				withTerminal = true;
			ui.SetState(State);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void Close()
		{
			if (ui.CurrentState == null)
				return;
			if (Terminal.PanelOpen)
				withTerminal = false;
			ui.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		// The terminal panel opened. If the index is already open it docks next to it, and closes with it from now on.
		public void OpenWithTerminal()
		{
			if (IsOpen)
				withTerminal = true;
			else if (withTerminal)
				ui.SetState(State);
		}

		// The terminal panel is closing (or switching to its deposit slots): a docked index goes with it.
		public void CloseWithTerminal()
		{
			if (IsOpen && Terminal.PanelOpen)
				ui.SetState(null);
		}

		public void ShowItem(int type, bool uses)
		{
			if (ui.CurrentState == null)
				Open();
			State.Show(type, uses);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			lastGameTime = gameTime;
			if (ui?.CurrentState == null)
				return;

			// Esc closes the panel, like the inventory (unless typing in the search box: then it only stops typing).
			// Next to the terminal, the terminal handles it and closes both.
			if (Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape) && !SearchBox.AnyFocused && !Terminal.PanelOpen) {
				Close();
				return;
			}
			ui.Update(gameTime);
			TakeHoveredWithMiddleClick();
		}

		// Middle click on any item in the index (the grid, or an ingredient in a recipe) takes a stack of it out of the
		// chests of the terminal in reach, the same one the index crafts from.
		private void TakeHoveredWithMiddleClick()
		{
			if (!Main.mouseMiddle || !Main.mouseMiddleRelease || HoveredItem <= 0 || !Main.LocalPlayer.mouseInterface)
				return;
			Main.mouseMiddleRelease = false;

			if (!Terminal.CanCraft) {
				Main.NewText(UIHelpers.T("TakeNeedsTerminal"), Color.Orange);
				return;
			}
			int amount = Math.Min(Terminal.StoredCount(HoveredItem), ContentSamples.ItemsByType[HoveredItem].maxStack);
			if (amount <= 0) {
				Main.NewText(UIHelpers.T("TakeNotStored", Lang.GetItemNameValue(HoveredItem)), Color.Orange);
				return;
			}
			Terminal.RequestTakeItems(new List<(int Type, int Amount)> { (HoveredItem, amount) });
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			// Before mouse text so item tooltips are drawn on top of the panel.
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index == -1)
				return;

			layers.Insert(index, new LegacyGameInterfaceLayer("TerraIndex: Browser", () => {
				if (ui?.CurrentState != null && lastGameTime != null)
					ui.Draw(Main.spriteBatch, lastGameTime);
				return true;
			}, InterfaceScaleType.UI));
		}

		public override void PostDrawInterface(SpriteBatch spriteBatch)
		{
			HoveredItem = Main.HoverItem?.type ?? 0;
		}
	}

	public class RecipeIndexPlayer : ModPlayer
	{
		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (SearchBox.AnyFocused)
				return;

			var system = ModContent.GetInstance<RecipeIndexSystem>();
			if (RecipeIndexSystem.ToggleKey.JustPressed)
				system.Toggle();

			int hovered = RecipeIndexSystem.HoveredItem;
			if (hovered > 0 && RecipeIndexSystem.RecipesKey.JustPressed)
				system.ShowItem(hovered, false);
			if (hovered > 0 && RecipeIndexSystem.UsesKey.JustPressed)
				system.ShowItem(hovered, true);
		}
	}
}
