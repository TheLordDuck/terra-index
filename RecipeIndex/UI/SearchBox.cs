using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;

namespace RecipeIndex.UI
{
	// Caja de texto: clic para escribir, clic derecho para borrar, Enter/Esc para salir.
	public class SearchBox : UIPanel
	{
		// Solo una caja puede tener el foco a la vez; las teclas del mod se ignoran mientras se escribe.
		private static SearchBox focusedBox;

		private readonly string hint;

		public static bool AnyFocused => focusedBox != null;

		public string Text { get; private set; } = "";
		public bool Focused => focusedBox == this;

		public event Action<string> OnTextChanged;

		public SearchBox(string hint)
		{
			this.hint = hint;
			SetPadding(6);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			Focus();
		}

		public void Focus()
		{
			focusedBox = this;
			Main.clrInput();
		}

		public void Unfocus()
		{
			if (focusedBox == this)
				focusedBox = null;
		}

		public override void RightClick(UIMouseEvent evt)
		{
			base.RightClick(evt);
			SetText("");
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (Focused && Main.mouseLeft && !IsMouseHovering)
				Unfocus();
		}

		public void SetText(string text)
		{
			if (text == Text)
				return;
			Text = text;
			OnTextChanged?.Invoke(text);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);

			if (Focused) {
				PlayerInput.WritingText = true;
				Main.instance.HandleIME();
				SetText(Main.GetInputText(Text));
				if (Main.inputTextEnter || Main.inputTextEscape)
					Unfocus();
			}

			CalculatedStyle inner = GetInnerDimensions();
			bool showHint = Text.Length == 0 && !Focused;
			string shown = showHint ? hint : Text + (Focused && Main.GameUpdateCount / 30 % 2 == 0 ? "|" : "");
			Utils.DrawBorderString(spriteBatch, shown, new Vector2(inner.X, inner.Y + 2), showHint ? Color.Gray : Color.White, 0.9f);
		}
	}
}
