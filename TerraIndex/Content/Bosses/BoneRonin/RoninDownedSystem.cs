using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// Whether the Bone Ronin has been beaten in this world: saved with the world and sent to clients.
	public class RoninDownedSystem : ModSystem
	{
		public override bool IsLoadingEnabled(Mod mod) => RoninContent.Enabled;

		public static bool downedBoneRonin;

		public override void ClearWorld()
		{
			downedBoneRonin = false;
		}

		public override void SaveWorldData(TagCompound tag)
		{
			if (downedBoneRonin)
				tag["downedBoneRonin"] = true;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			downedBoneRonin = tag.ContainsKey("downedBoneRonin");
		}

		public override void NetSend(BinaryWriter writer)
		{
			var flags = new BitsByte();
			flags[0] = downedBoneRonin;
			writer.Write(flags);
		}

		public override void NetReceive(BinaryReader reader)
		{
			BitsByte flags = reader.ReadByte();
			downedBoneRonin = flags[0];
		}
	}
}
