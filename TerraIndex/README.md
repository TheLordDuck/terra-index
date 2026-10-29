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
  potions and consumables, materials (ores and bars too), and miscellaneous (hover a tab for its name and count).
- **Calamity guide** (only with the Calamity Mod enabled): the gold button next to the title (or the book tab,
  second) shows the gear the Calamity wiki's [class setups guide](https://calamitymod.wiki.gg/wiki/Guide:Class_setups)
  recommends for your class at your point of the game: armor first, then weapons, accessories and potions. Pick
  your class (melee, ranged, magic, summoner or rogue) with the class button next to the search; it is saved with
  the character, and the first time it takes your held weapon's class. Your stage comes from the bosses beaten in
  the world (before the Desert Scourge, before the Eater of Worlds / Brain of Cthulhu... up to the endgame), and
  when a kill moves you to the next stage, the chat tells you and names some of the new gear. Click an item to see
  its recipe with what you have (green), what is in the chests (blue) and what is missing (red).
- The item list is split into pages: use the buttons at the bottom, the mouse wheel (Shift: 10 pages at a time),
  or drag the page bar.
- Esc closes the index.
- Search box: type a name, or `@calamity` / `@terraria` to filter by mod. Right click clears it.
- Class button (next to the search box): only the weapons of one class: melee, ranged, magic, summoner, throwing,
  and the classes mods add, such as Calamity's **rogue**. Left click the next class, right click the previous one.
  It works together with the search and the tabs.
- Ingredients are green if you have enough in your inventory, red if not (blue when they are in a storage
  terminal's chests, see below).
- Items that can't be crafted show **how to obtain** them: who sells them (and the price), which enemies drop them
  (with their picture, the chance and where they spawn), and which bags or crates contain them.

### Crafting from your chests

After using a storage terminal, while you stay in its reach (or carry a Portable Terminal linked to it), the recipe
index can craft straight from the terminal's chests:

- At the bottom of a recipe: how many you can make from the chests and your inventory, and **Craft** / **x10** / **Max** buttons
  (up to 999 crafts at once, or 20 for items that don't stack). The result comes out of the terminal's door, or
  appears next to you when you are far away with the Portable Terminal.
- Crafting stations count if they are **near you** (as usual) **or touched by the terminal's red wire**: wire your
  anvil, furnace, etc. into the network and you can craft from anywhere with the Portable Terminal. A Hellforge
  also counts as a Furnace, a Mythril Anvil as an Anvil, and so on.
- Special conditions (near water, lava, honey, snow biome...) are checked where you stand.
- Every item's tooltip (in your inventory, chests, shops...) says **"In your chests: N"** while the terminal is in
  reach. It can be turned off in the settings.
- Ingredients are taken **from the chests first**, and whatever they lack **from your inventory**, so you can mix
  both (e.g. 5 bars in the chests and 5 in your pocket for a 10-bar recipe).
- **Take missing** (above the craft buttons) does the opposite: it takes out of the chests whatever your inventory
  lacks for one craft (Shift + click: for 10), so you can craft it the usual way. It only appears when the chests
  hold some of what is missing.
- Ingredient colors: green = enough in your inventory, **blue = enough in the terminal's chests** (or in the chests
  and your inventory together), red = not enough.
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

The goal panel also has a button for the **next step**: it crafts whatever can be made now on the way to the goal
("Craft 3 x Iron Bar", then "Craft 1 x Iron Broadsword"), from your inventory with the station nearby, or from the
chests of a terminal in reach. If a step only lacks its crafting station, the panel says which one.

## Storage terminal

1. Buy the **Terminal** from the **Steampunker** (1 platinum by default; the host can change it in the settings).
2. Place it on a **background wall** (like a painting, it is 4 wide and 2 tall: a screen on the left and a garage door on the right).
3. Run **red wire** over the terminal and over every chest you want to connect. Everything must be on the same
   wire: the network is walked starting from the terminal, so if the wire doesn't touch it, no chests are found.
4. **Right click** the terminal and type: the list filters as you type.
5. **Click** an item: the garage door rolls up and a stack of it drops out (taken from the connected chests).
   To take a specific amount, **right click** it like in your inventory: each right click picks up 1 more, and
   holding it counts up faster and faster. The amount follows the cursor; click any item to take that many out,
   or right click empty space to cancel.
6. **Shift + click**: the chests holding it glow for 10 seconds, and an arrow on the screen edge points to any
   that are off screen. **Ctrl + click**: shows that item's recipes in the index, next to the terminal.
7. **Deposit items** (button at the top of the panel): opens 20 slots under your inventory, like a chest. Drag items
   in, or Shift + click them in your inventory. When you close it, the door opens and the items go into the
   connected chests (first onto matching stacks, then into empty slots). If the chests are full you get a warning
   and what didn't fit stays in the terminal: it is retried every 2 seconds, and you can take it back out by
   opening the slots again. Breaking the terminal drops anything left in it.
8. **Deposit all** (next to Deposit items): sends your whole main inventory straight into the chests. The hotbar,
   coins, ammo and favorited items stay with you; anything that doesn't fit is given back.
9. **Sort** (button above the list): cycles between name, amount, rarity and mod.
10. **Recipes** (next to Sort): opens the recipe index next to the terminal, so you can browse recipes and craft
    from the chests without switching panels. Taking items out no longer closes the terminal. While it is there it opens and closes together with the terminal;
    close it with its X to stop that.
11. **?**: hover it to see all these controls. The index has one too.
12. Above the items, the **crafting stations on the terminal's wire** (hover one for its name). They count for
    crafting from the chests, as if they were next to you.

What you take out drops out of the terminal's door (only you can pick it up for 10 seconds), and the chests it came
from spark. With a portable terminal far away, it appears at your feet instead. Reopening the same
terminal within 30 seconds keeps your search. In the recipe index, **middle click** any item to take a stack of it out
of the chests of the terminal you are near.

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

## Blueprints

Craft a **Blueprint** (10 of any wood at a work bench) to build ready-made houses:

1. Hold it and **right click**: pick a design and its material (every wood, glass and more, including woods from
   other mods): small house, two-floor house, tower, **medieval house**, **castle**, **pagoda**, **wizard tower**,
   **craftsman's house** (a forge yard, kitchen, workshop hall, library, bedroom and attic with a place for every
   crafting station), **tree village** (a big lodge with 30 NPC rooms between two big trees, to go above the
   craftsman's house), **big tree** (a living wood tree from halfway up its trunk: branches that curve out and
   thin down into clusters of leaves, with vines; put it on top of a trunk you build, or on the ground), **thick tree** (the same
   tree with a trunk and branches two tiles thicker), boss arena, **herb garden** (an alchemist's hut and three rows
   of planter boxes for your seeds), **fishing pond** (300 tiles of water, just what fishing needs, a pier and a fisherman's hut) or **arena
   with buffs** (four rows of platforms with campfires, heart lanterns and stars in a bottle). The big ones mix fixed materials (gray brick, red brick or shingle roofs, glass windows, banners,
   fireplaces) with the wood you pick for the frame and the furniture. Pictures of all of them are in
   [`docs/blueprints/`](../docs/blueprints/). **Place** closes the catalog.
   The catalog's **Town NPCs** tab shows every town NPC's head: click one to see the biomes and neighbours it
   loves, likes, dislikes and hates, as icons (hover one for its name, click a neighbour to see theirs), so you know
   where to build its house.
2. Move the mouse: a see-through preview shows where it goes, standing on the tile under the mouse. Red tiles mean
   something is in the way (grass and plants are cleared on their own).
3. **Left click** to build it. It's free: the Blueprint itself is all it costs. Walls already there are kept. Every
   room is valid housing for an NPC.

## Wireless teleporters

Teleporters that need no wire:

1. Craft a **Wireless Teleporter** (a Teleporter and 10 Wire at the Tinkerer's Workshop) and place at least two on
   the floor (3 wide, you can stand on them).
2. Craft a **Teleporter Linker** (a Red Wrench and 3 Iron or Lead Bars at an anvil). Hold it and **right click** a
   teleporter: a menu lists the channels already in use (and how many teleporters each has). Pick one, or make a
   **new channel**. Do the same on the other teleporter, picking the same channel.
3. **Right click** a teleporter to go to the next one on its channel. No pressure plate or wire needed.

Breaking a teleporter doesn't touch the others: they stay on their channel, so a new one put on it works right away.
With three or more on a channel, right clicking goes round them in a loop. Hovering a teleporter shows its channel.
Channels are saved with the world and work in multiplayer.

## Multiplayer

- The mod must be installed on the server and on every client (tModLoader syncs it automatically on join).
- The server scans the chests and does every withdrawal, deposit and craft, since it is the only one that knows
  every chest. While the panel is open it refreshes every half second, so you see changes made by other players.
- Crafting takes all the ingredients out of the chests or none, so two players crafting at once can't spend the
  same items twice. What comes from your inventory is taken out before asking the server, and given back if the
  craft fails. Only one player at a time can use a terminal's deposit slots.
- Crafting goals, the goal panel and your settings are per player; server settings are chosen by the host.

## Settings

In **Settings → Mod Configuration → Terra Index**:

| Your settings (each player) | Default |
|---|---|
| Show the goal panel, and how many materials it lists | On, 8 |
| Default terminal sort when entering a world | Name |
| Chest highlight duration | 10 s |
| Include the hotbar in "Deposit all" | Off |
| Type in the terminal's search box when it opens | On |
| Chest effects (sparks, light, sound) | On |
| "In your chests" in item tooltips | On |

| Server settings (the host's, synced to everyone) | Default |
|---|---|
| Terminal price at the Steampunker | 100 gold (1 platinum) |
| Terminal reach before its panel closes | 10 blocks |
| Maximum wire network size | 50,000 wire tiles |
| Allow the Portable Terminal | On |

## Changelog

**1.1**
- Wireless teleporters: put them on a channel with the Teleporter Linker and right click one to go to the next one
  on its channel, no wire needed.
- Target Dummy damage meter, like the League of Legends practice dummy: total damage, DPS and last combo of each fight.
- New blueprints: herb garden, fishing pond (it comes with its 300 tiles of water) and arena with buffs.
- The blueprint catalog has two tabs, buildings and town NPCs: the second shows what each town NPC loves, likes,
  dislikes and hates, as biome icons and NPC heads.
- **Calamity guide** in the recipe index (gold button, only with Calamity): the wiki's recommended gear for your
  class at your stage, with a chat message when a boss kill brings new gear. It replaces the Next upgrades tab.
- The recipe index filters by class (melee, ranged, magic, summoner, throwing, Calamity's rogue and other mods' classes).
- Item tooltips say how many are in your chests while a terminal is in reach.

**1.0**: first release on the Steam Workshop, with everything from 0.9.

**0.9**
- Crafting from the terminal also uses your inventory: the chests first, then what they lack from your inventory.
- The terminal shows the crafting stations on its wire, and its free slots and how full the chests are.
- Ores and bars are in the materials tab, not with the blocks and furniture.
- What you take out drops out of the terminal's door, reserved for you for 10 seconds; drones only bring items to
  a portable terminal far from the door.
- Blueprints are free: the Blueprint itself is all they cost.
- New blueprints: craftsman's house (a themed room and a place for every crafting station), tree village (30 NPC
  rooms), big tree and thick tree.
- Fixed translations whose text starts with a placeholder breaking the language files.

**0.8**
- Blueprints: pick a design and a material, see a preview where it goes and build it with one click, with
  materials from your inventory (or brought from the terminal's chests). Every room is valid NPC housing.
  Designs: small house, two-floor house, tower, medieval house, castle, pagoda, wizard tower, boss arena.

**0.7**
- The recipe index opens next to the terminal (Recipes button or Ctrl + click), and both can be used at once.
  The terminal stays open when taking items out and on "Deposit all".
- Middle click an item in the recipe index to take a stack out of the chests. The terminal remembers your search for
  30 s, and typing in it on open can be turned off.
- Goal panel: a button crafts the next step towards the goal (first the bars, then the sword), from your inventory
  or from a terminal's chests.
- Chests spark when items are taken out of them.
- German, French, Brazilian Portuguese, Russian, Italian, Polish and Simplified Chinese.
- "?" buttons with the controls of the terminal and the index; tips the first time you place and open a terminal.

**0.6**
- Radio: crafted at an anvil, placed like a Piggy Bank; plays two clips at random moments, the same for every
  player in multiplayer, with music notes floating out while it plays. Right click: on/off.

**0.5**
- Terminal: hold right click on an item to pick an amount (like in your inventory), then click to take it out.
  Replaces the "how many?" pop-up. Ctrl + click opens the recipe.
- Performance: the terminal and the recipe panels skip work when nothing changed.
- Fixed the build error in 0.4.

**0.4**
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
