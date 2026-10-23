using System.ComponentModel;
using TerraIndex.Content.Storage;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace TerraIndex.Common
{
	// Per-player settings (Settings > Mod Configuration). Labels and tooltips are in the localization files.
	public class ClientConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static ClientConfig Instance => ModContent.GetInstance<ClientConfig>();

		[Header("Goal")]
		[DefaultValue(true)]
		public bool ShowGoalPanel;

		[Range(1, 20)]
		[DefaultValue(8)]
		public int GoalPanelRows;

		[Header("Terminal")]
		[DefaultValue(TerminalSort.Name)]
		public TerminalSort DefaultSort;

		[Range(1, 60)]
		[DefaultValue(10)]
		public int HighlightSeconds;

		[DefaultValue(false)]
		public bool QuickDepositHotbar;

		[DefaultValue(true)]
		public bool FocusTerminalSearch;

		[Header("Effects")]
		[DefaultValue(true)]
		public bool DeliveryDrones;

		[DefaultValue(true)]
		public bool ChestEffects;
	}

	// Gameplay settings chosen by the host and synced to every player.
	public class ServerConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		public static ServerConfig Instance => ModContent.GetInstance<ServerConfig>();

		[Range(1, 1000)]
		[DefaultValue(100)]
		public int TerminalPriceGold;

		[Range(3, 100)]
		[DefaultValue(10)]
		public int TerminalReach;

		[Range(1000, 500000)]
		[Increment(1000)]
		[DefaultValue(50000)]
		public int MaxWireTiles;

		[DefaultValue(true)]
		public bool AllowPortableTerminal;
	}
}
