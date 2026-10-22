using System.IO;
using TerraIndex.Content.Storage;
using Terraria.ModLoader;

namespace TerraIndex
{
	public class TerraIndex : Mod
	{
		public override void HandlePacket(BinaryReader reader, int whoAmI) => TerminalNet.Handle(reader, whoAmI);
	}
}
