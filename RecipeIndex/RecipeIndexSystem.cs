using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RecipeIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace RecipeIndex
{
	public class RecipeIndexSystem : ModSystem
	{
		public static ModKeybind ToggleKey { get; private set; }
		public static ModKeybind RecipesKey { get; private set; }
		public static ModKeybind UsesKey { get; private set; }

		// Objeto bajo el ratón en el último frame (inventario, cofres, nuestra cuadrícula...).
		public static int HoveredItem { get; private set; }

		private UserInterface ui;
		private BrowserState state;
		private GameTime lastGameTime;

		public override void Load()
		{
			ToggleKey = KeybindLoader.RegisterKeybind(Mod, "ToggleBrowser", Keys.N);
			RecipesKey = KeybindLoader.RegisterKeybind(Mod, "ShowRecipes", Keys.OemOpenBrackets);
			UsesKey = KeybindLoader.RegisterKeybind(Mod, "ShowUses", Keys.OemCloseBrackets);

			if (!Main.dedServ) {
				ui = new UserInterface();
				state = new BrowserState();
				state.Activate();
			}
		}

		public override void Unload()
		{
			ToggleKey = RecipesKey = UsesKey = null;
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
			ui.SetState(state);
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
			state.Show(type, uses);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			lastGameTime = gameTime;
			if (ui?.CurrentState != null)
				ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			// Antes del texto del ratón para que los tooltips de los objetos se dibujen encima del panel.
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
