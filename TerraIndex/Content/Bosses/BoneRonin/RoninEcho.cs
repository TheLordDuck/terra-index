using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TerraIndex.Content.Bosses.BoneRonin
{
	// The Bone Ronin's echo: from phase 2, a see-through blue copy of him that repeats each of his attacks a moment
	// later (1 second), from the other side of the player. All its behaviour is BoneRonin's with
	// IsEcho on; here it only stops being a boss: it can't be hurt, drops nothing, has no health bar, map icon or
	// bestiary entry, and leaves when he dies or leaves.
	public class RoninEcho : BoneRonin
	{
		public override string Texture => "TerraIndex/Content/Bosses/BoneRonin/BoneRonin";
		public override string BossHeadTexture => "TerraIndex/Content/Bosses/BoneRonin/BoneRonin_Head_Boss";

		protected override bool IsEcho => true;

		public override void BossHeadSlot(ref int index) => index = -1;

		public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

		public override void ModifyNPCLoot(NPCLoot npcLoot) { }

		public override void OnKill() { }

		public override bool CheckDead() => true;
	}
}
