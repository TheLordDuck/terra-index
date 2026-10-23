using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;

namespace TerraIndex.UI
{
	// "?" button for a panel's header: hovering it shows the panel's controls as a tooltip.
	public class HelpButton : UITextPanel<string>
	{
		private readonly string textKey;

		public HelpButton(string textKey) : base("?")
		{
			this.textKey = textKey;
			Width.Set(34, 0);
			Height.Set(30, 0);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			if (IsMouseHovering) {
				Main.LocalPlayer.mouseInterface = true;
				Main.hoverItemName = UIHelpers.T(textKey);
			}
		}
	}
}
