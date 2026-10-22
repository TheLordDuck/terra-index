# Terra Index

NEI-style recipe browser, crafting planner and storage terminal for Terraria (tModLoader 1.4.4): search any
recipe, plan what you are missing, and craft straight from your chests.

## Installation (development)

1. Install **tModLoader** from Steam and launch it once.
2. Copy (or link) this `TerraIndex` folder into
   `Documents\My Games\Terraria\tModLoader\ModSources\`.
   To link instead of copying (PowerShell as administrator):
   `New-Item -ItemType Junction -Path "$HOME\Documents\My Games\Terraria\tModLoader\ModSources\TerraIndex" -Target "<path to this folder>"`
3. In tModLoader: **Workshop → Develop Mods → TerraIndex → Build + Reload**.

**Coming from "Recipe Index":** the mod was renamed to Terra Index (internal name `TerraIndex`). Remove the old
`ModSources\RecipeIndex` link, create `ModSources\TerraIndex` as above, disable any old `RecipeIndex` in the mod
list, and rebind the N / [ / ] keys if you had changed them.

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
- Ingredients are green if you have enough in your inventory, red if not (blue when they are in a storage
  terminal's chests, see below).
- Items that can't be crafted show **how to obtain** them: who sells them (and the price), which enemies drop them
  (with their picture, the chance and where they spawn), and which bags or crates contain them.

### Crafting from your chests

After using a storage terminal, while you stay in its reach (or carry a Portable Terminal linked to it), the recipe
index can craft straight from the terminal's chests:

- At the bottom of a recipe: how many you can make from the chests, and **Craft** / **x10** / **Max** buttons
  (up to 999 crafts at once, or 20 for items that don't stack). The result comes out of the terminal's door, or
  appears next to you when you are far away with the Portable Terminal.
- Crafting stations count if they are **near you** (as usual) **or touched by the terminal's red wire**: wire your
  anvil, furnace, etc. into the network and you can craft from anywhere with the Portable Terminal. A Hellforge
  also counts as a Furnace, a Mythril Anvil as an Anvil, and so on.
- Special conditions (near water, lava, honey, snow biome...) are checked where you stand.
- Ingredients are taken **only from the chests**, not from your inventory (use **Deposit all** first if needed).
- **Take missing** (above the craft buttons) does the opposite: it takes out of the chests whatever your inventory
  lacks for one craft (Shift + click: for 10), so you can craft it the usual way. It only appears when the chests
  hold some of what is missing.
- Ingredient colors: green = enough in your inventory, **blue = enough in the terminal's chests**, red = not enough.
  Stations turn green or red too.

### Comparing items

- **+ Compare** (top right of the recipe panel) adds the item you are looking at to a comparison of up to 3 items
  (adding a fourth drops the oldest). Once added, the button shows **Compare (n)** and opens the comparison.
- Side by side: damage, critical chance, use time, knockback, mana cost, projectile speed, pickaxe / axe / hammer
  power, defense and sell value (only the rows that apply), with the best value in green. Below, each item's
  tooltip text, which is where accessory effects are described.
- Click an item in the comparison to open it; right click it to remove it.

### Crafting tree and goal

- **Tree** tab (next to Recipes / Uses): the full crafting tree of the recipe you were looking at, down to the
  base materials, using what is already in your inventory first. Green: you have enough. White: crafted from the
  rows below it. Red: a base material you are missing. Below the tree: the missing materials added up, and every
  crafting station you need. Mouse wheel: scroll. Click a row to open that item.
- **Set as goal** (in the Tree view): a small panel on the left of the screen lists the materials still missing,
  updated as you pick things up, and a chat message tells you when you have everything. Drag its title to move it,
  and use the **-** / **+** button to fold it (folded, the title turns green when everything is ready). It is
  hidden while the inventory is open, saved with your character (goal, position and folding), and removed with
  **Remove goal**.
- Intermediate items use their first recipe that doesn't loop back (for example Wood is never "crafted" from Wood
  Platforms); items with no such recipe count as base materials.

## Storage terminal

1. Buy the **Terminal** from the **Steampunker** (1 platinum by default; the host can change it in the settings).
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
8. **Deposit all** (next to Deposit items): sends your whole main inventory straight into the chests. The hotbar,
   coins, ammo and favorited items stay with you; anything that doesn't fit is given back.
9. **Sort** (button above the list): cycles between name, amount, rarity and mod.

Closes with Esc, the X button, or by walking away.

### Portable terminal

Crafted at the **Ancient Manipulator** from a Terminal and 10 Luminite Bars.

1. Hold it and **right click** a terminal to link them (its tooltip shows how far away the linked terminal is).
2. **Use** it anywhere in that world to open the terminal. Everything works as usual, except that items you take
   out appear next to you instead of coming out of the door.
3. The panel stays open while you carry the portable terminal. Highlighted chests still get their arrows on the
   screen edge, so it also works as a compass to find them.
4. With your crafting stations on the terminal's wire, you can also craft from your chests from anywhere.

The host can turn portable terminals off in the server settings.

## Multiplayer

- The mod must be installed on the server and on every client (tModLoader syncs it automatically on join).
- The server scans the chests and does every withdrawal, deposit and craft, since it is the only one that knows
  every chest. While the panel is open it refreshes every half second, so you see changes made by other players.
- Crafting takes all the ingredients out of the chests or none, so two players crafting at once can't spend the
  same items twice. Only one player at a time can use a terminal's deposit slots.
- Crafting goals, the goal panel and your settings are per player; server settings are chosen by the host.

## Settings

In **Settings → Mod Configuration → Terra Index**:

| Your settings (each player) | Default |
|---|---|
| Show the goal panel, and how many materials it lists | On, 8 |
| Default terminal sort when entering a world | Name |
| Chest highlight duration | 10 s |
| Include the hotbar in "Deposit all" | Off |

| Server settings (the host's, synced to everyone) | Default |
|---|---|
| Terminal price at the Steampunker | 100 gold (1 platinum) |
| Terminal reach before its panel closes | 10 blocks |
| Maximum wire network size | 50,000 wire tiles |
| Allow the Portable Terminal | On |

## Changelog

**0.4** (not yet tested in game)
- Mod renamed from Recipe Index to Terra Index.
- Crafting from the terminal's chests, with stations near you or on its wire; "Take missing" ingredients;
  blue ingredients when they are in the chests.
- Crafting tree, crafting goal with a movable, foldable on-screen panel.
- Item comparison.
- Terminal: Deposit all, sorting (name, amount, rarity, mod).
- Portable terminal.
- Settings for each player and for the server.

**0.3**: amount prompt when taking items out of the terminal; item category tabs.

**0.2**: paginated item grid, Esc to close, enemy sprites and spawn locations in "how to obtain".

**0.1**: recipe index, storage terminal with garage door, deposit slots, item sources, multiplayer.

## Publishing to the Steam Workshop

1. Build the mod (see Installation) and test it in a world.
2. In tModLoader: **Workshop → Develop Mods → Publish** on TerraIndex (logged into Steam).
3. Pick the visibility: **Public**, **Friends only** or **Private**.
4. Share the Workshop link; friends click **Subscribe** and enable the mod in **Workshop → Manage Mods**.

The Workshop page uses `description.txt` (description), `icon.png` (icon) and `build.txt`
(name, author, version, homepage).

**Updating:** bump `version` in `build.txt` (e.g. `0.1` → `0.2`), rebuild, and click **Publish** again.
Subscribers get the update automatically.
