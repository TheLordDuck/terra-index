using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TerraIndex.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TerraIndex.Common
{
	// The Calamity guide book's window (GuideBookState), opened with the guide book item.
	public class GuideBookSystem : ModSystem
	{
		private UserInterface ui;
		private GameTime lastGameTime;

		public bool IsOpen => ui?.CurrentState != null;

		public override void Load()
		{
			if (!Main.dedServ)
				ui = new UserInterface();
		}

		public override void Unload() => GuideBookState.Unload();

		public override void OnWorldUnload() => ui?.SetState(null);

		public void Toggle()
		{
			if (IsOpen)
				Close();
			else
				Open();
		}

		// Built each time it opens: it starts at the world's stage, for the class then.
		public void Open()
		{
			ui.SetState(UIHelpers.Build(new GuideBookState()));
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public void Close()
		{
			if (!IsOpen)
				return;
			ui.SetState(null);
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			lastGameTime = gameTime;
			if (ui?.CurrentState == null)
				return;
			if (Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape)) {
				Close();
				return;
			}
			ui.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			if (index == -1)
				return;
			layers.Insert(index, UIHelpers.Layer("TerraIndex: Guide book", () => {
				if (ui?.CurrentState != null && lastGameTime != null)
					ui.Draw(Main.spriteBatch, lastGameTime);
				return true;
			}));
		}
	}
}
