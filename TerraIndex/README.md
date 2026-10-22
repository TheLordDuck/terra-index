# Terra Index

NEI-style recipe browser and storage terminal for Terraria (tModLoader 1.4.4).

## Installation (development)

1. Install **tModLoader** from Steam and launch it once.
2. Copy (or link) this `TerraIndex` folder into
   `Documents\My Games\Terraria\tModLoader\ModSources\`.
   To link instead of copying (PowerShell as administrator):
   `New-Item -ItemType Junction -Path "$HOME\Documents\My Games\Terraria\tModLoader\ModSources\TerraIndex" -Target "<path to this folder>"`
3. In tModLoader: **Workshop → Develop Mods → TerraIndex → Build + Reload**.

## Recipe index

| Key (rebindable in Controls) | Action |
|---|---|
| `N` | Open/close the index |
| `[` while hovering an item | Show its recipes |
| `]` while hovering an item | Show what it is used in |

- Left click an item: recipes. Right click: uses.
- Inside a recipe you can click ingredients and crafting stations to navigate; **Back** returns.
- Mouse wheel over the right panel: switch recipe.
- Tabs above the items split them by type: all, weapons and ammo, tools, armor, accessories, blocks and furniture,
  potions and consumables, materials, and miscellaneous (hover a tab for its name and count).
- The item list is split into pages: use the buttons at the bottom, the mouse wheel (Shift: 10 pages at a time),
  or drag the page bar.
- Esc closes the index.
- Search box: type a name, or `@calamity` / `@terraria` to filter by mod. Right click clears it.
- Ingredients are green if you have enough in your inventory, red if not.
- Items that can't be crafted show **how to obtain** them: who sells them (and the price), which enemies drop them
  (with their picture, the chance and where they spawn), and which bags or crates contain them.

## Storage terminal

1. Buy the **Terminal** from the **Steampunker** (1 platinum).
2. Place it on a **background wall** (like a painting, it is 4 wide and 2 tall: a screen on the left and a garage door on the right).
3. Run **red wire** over the terminal and over every chest you want to connect. Everything must be on the same
   wire: the network is walked starting from the terminal, so if the wire doesn't touch it, no chests are found.
4. **Right click** the terminal and type: the list filters as you type.
5. **Click** an item: the garage door rolls up and a stack of it drops out (taken from the connected chests).
   For stackable items a pop-up asks how many first: type a number, use -/+ (Shift: 10) or 1 / 10 / Max, then
   Enter or **Take out** (empty = as many as fit in one stack). Esc or **Cancel** closes it.
6. **Shift + click**: the chests holding it glow for 10 seconds, and an arrow on the screen edge points to any
   that are off screen. **Right click**: opens that item's recipe in the index.
7. **Deposit items** (button at the top of the panel): opens 20 slots under your inventory, like a chest. Drag items
   in, or Shift + click them in your inventory. When you close it, the door opens and the items go into the
   connected chests (first onto matching stacks, then into empty slots). If the chests are full you get a warning
   and what didn't fit stays in the terminal: it is retried every 2 seconds, and you can take it back out by
   opening the slots again. Breaking the terminal drops anything left in it.

Closes with Esc, the X button, or by walking away.

**Multiplayer:** the mod must be installed on the server and on every client (tModLoader syncs it automatically
on join). The server does the scan and the withdrawing, since it is the only one that knows every chest, and while the panel is open
it refreshes every half second, so you see changes made by other players.

## Publishing to the Steam Workshop

1. Build the mod (see Installation) and test it in a world.
2. In tModLoader: **Workshop → Develop Mods → Publish** on TerraIndex (logged into Steam).
3. Pick the visibility: **Public**, **Friends only** or **Private**.
4. Share the Workshop link; friends click **Subscribe** and enable the mod in **Workshop → Manage Mods**.

The Workshop page uses `description.txt` (description), `icon.png` (icon) and `build.txt`
(name, author, version, homepage).

**Updating:** bump `version` in `build.txt` (e.g. `0.1` → `0.2`), rebuild, and click **Publish** again.
Subscribers get the update automatically.
