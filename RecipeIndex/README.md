# Recipe Index

NEI-style recipe browser and storage terminal for Terraria (tModLoader 1.4.4).

## Installation (development)

1. Install **tModLoader** from Steam and launch it once.
2. Copy (or link) this `RecipeIndex` folder into
   `Documents\My Games\Terraria\tModLoader\ModSources\`.
   To link instead of copying (PowerShell as administrator):
   `New-Item -ItemType Junction -Path "$HOME\Documents\My Games\Terraria\tModLoader\ModSources\RecipeIndex" -Target "<path to this folder>"`
3. In tModLoader: **Workshop → Develop Mods → RecipeIndex → Build + Reload**.

## Recipe index

| Key (rebindable in Controls) | Action |
|---|---|
| `N` | Open/close the index |
| `[` while hovering an item | Show its recipes |
| `]` while hovering an item | Show what it is used in |

- Left click an item: recipes. Right click: uses.
- Inside a recipe you can click ingredients and crafting stations to navigate; **Back** returns.
- Mouse wheel over the right panel: switch recipe.
- Search box: type a name, or `@calamity` / `@terraria` to filter by mod. Right click clears it.
- Ingredients are green if you have enough in your inventory, red if not.

## Storage terminal

1. Buy the **Terminal** from the **Mechanic** (5 silver).
2. Place it on a **background wall** (like a painting, it is 2x2).
3. Run **red wire** over the terminal and over every chest you want to connect. Everything must be on the same
   wire: the network is walked starting from the terminal, so if the wire doesn't touch it, no chests are found.
4. **Right click** the terminal and type: the list filters as you type.
5. **Click** an item: the chests holding it glow for 10 seconds, and an arrow on the screen edge points to any
   that are off screen. **Right click**: opens that item's recipe in the index.

Closes with Esc, the X button, or by walking away.

**Multiplayer:** the mod must be installed on the server and on every client (tModLoader syncs it automatically
on join). The server does the scan, since it is the only one that knows every chest, and while the panel is open
it refreshes every half second, so you see changes made by other players.

## Publishing to the Steam Workshop

1. Build the mod (see Installation) and test it in a world.
2. In tModLoader: **Workshop → Develop Mods → Publish** on RecipeIndex (logged into Steam).
3. Pick the visibility: **Public**, **Friends only** or **Private**.
4. Share the Workshop link; friends click **Subscribe** and enable the mod in **Workshop → Manage Mods**.

The Workshop page uses `description.txt` (description), `icon.png` (icon) and `build.txt`
(name, author, version, homepage).

**Updating:** bump `version` in `build.txt` (e.g. `0.1` → `0.2`), rebuild, and click **Publish** again.
Subscribers get the update automatically.
