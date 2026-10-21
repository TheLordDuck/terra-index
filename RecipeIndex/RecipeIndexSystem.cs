using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RecipeIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace RecipeIndex
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
			ui.SetState(State);
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void Close()
		{
			ui.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
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
			if (ui?.CurrentState != null)
				ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			// Before mouse text so item tooltips are drawn on top of the panel.
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index == -1)
				return;

			layers.Insert(index, new LegacyGameInterfaceLayer("RecipeIndex: Browser", () => {
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
