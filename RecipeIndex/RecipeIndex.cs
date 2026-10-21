using System.IO;
using RecipeIndex.Content.Storage;
using Terraria.ModLoader;

namespace RecipeIndex
{
	public class RecipeIndex : Mod
	{
		public override void HandlePacket(BinaryReader reader, int whoAmI) => TerminalNet.Handle(reader, whoAmI);
	}
}
