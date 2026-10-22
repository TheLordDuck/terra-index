# Testing checklist (version 0.4)

Everything in 0.4 was written on a PC without tModLoader: none of it has been compiled or tested in game yet.
Tick each box as you go. Multiplayer checks need a second player (Host & Play).

## 0. Setup

- [ ] `git pull`
- [ ] Remove the old `ModSources\RecipeIndex` link and create `ModSources\TerraIndex` (command in `TerraIndex/README.md`)
- [ ] tModLoader: **Develop Mods → TerraIndex → Build + Reload** (copy any build errors to fix them)
- [ ] Disable the old `RecipeIndex` in the mod list if it still shows up
- [ ] Rebind N / [ / ] if you had changed them (keybinds are stored under the mod's internal name)

## 1. Crafting from the chests

- [ ] With a terminal and wired chests: open the index (N), pick a recipe, and the bottom row says
      "From your chests you can make N"
- [ ] **Craft**, **x10** and **Max** take the ingredients from the chests and the item comes out of the door
- [ ] A crafted weapon gets a random prefix
- [ ] Crafting station **near you**: works without wiring it
- [ ] Crafting station **far away but on the red wire**: also works
- [ ] A Hellforge on the wire counts as a Furnace; a Mythril Anvil as an Anvil
- [ ] Without the station: "Missing crafting station" message
- [ ] Recipe with a group ingredient ("Any Wood"): another kind of wood is accepted
- [ ] Walk away from the terminal: the craft row disappears
- [ ] **Multiplayer:** both players crafting at once with the same materials don't duplicate anything

## 2. Blue ingredients

- [ ] Ingredient in the inventory → green; only in the chests → **blue**; in neither → red
- [ ] Crafting stations turn green or red near a terminal

## 4. Crafting tree

- [ ] **Tree** tab on a complex item (Terra Blade, an armor piece...)
- [ ] Bars you already have aren't asked for again
- [ ] Wood is a base material (not "crafted from Wood Platforms")
- [ ] Below the tree: "Missing materials" added up and "Crafting stations"
- [ ] Mouse wheel scrolls; clicking a row opens that item
- [ ] Items with several recipes say "(recipe 2 of 3)"

## 5 and 18. Crafting goal and its panel

- [ ] **Set as goal** in the tree: the panel appears on the left
- [ ] Pick up a material: its count goes up on its own
- [ ] Once you have everything: chat message "You have everything to craft ...!"
- [ ] **Drag** the panel by its title
- [ ] The **-** / **+** button folds it; folded, the title turns green when ready
- [ ] Hidden while the inventory is open
- [ ] Leave and rejoin: goal, position and folding are kept
- [ ] **Remove goal** removes it

## 6. Terminal sorting

- [ ] The "Sort: Name" button cycles Amount → Rarity → Mod
- [ ] Each order looks right (amount from most to least, vanilla first when sorting by mod)

## 8. Deposit all

- [ ] **Deposit all** puts the inventory into the chests, with items flying into the door
- [ ] The hotbar, coins, ammo and favorited items stay
- [ ] With full chests: what doesn't fit is given back, with a warning
- [ ] Empty inventory: "Nothing to deposit" message
- [ ] **Multiplayer:** no items are lost

## 10. Portable terminal

- [ ] Craft it (Terminal + 10 Luminite Bars at the Ancient Manipulator)
- [ ] Hold it and right click a terminal: "linked", and the tooltip shows the distance
- [ ] Using it far away opens the terminal
- [ ] Taking an item out far away: it appears next to you
- [ ] Shift + click: arrows point to the far chests
- [ ] Drop it or stash it: the panel closes
- [ ] Not linked: warning message
- [ ] **Multiplayer:** works far from the base (the server accepts the requests)

## 13. Item comparison

- [ ] **+ Compare** on 2-3 weapons: table with the best value in green
- [ ] Comparing accessories: their effects (tooltip text) appear below
- [ ] Adding a fourth item drops the oldest
- [ ] Right click an item in the comparison removes it

## 17. Take missing

- [ ] On a recipe with ingredients in the chests: the "In the chests, missing from your inventory" row and **Take missing**
- [ ] It only takes what is missing (carrying 2 of 5 takes 3)
- [ ] **Shift + click** takes enough for 10 crafts
- [ ] With everything in the inventory, the row doesn't show

## 27. Settings

- [ ] **Settings → Mod Configuration → Terra Index**: texts look right in Spanish
- [ ] Change the **price** and open the Steampunker's shop: new price
- [ ] Change the **reach**: the panel closes at that distance
- [ ] Turn the **portable terminal** off: it says it is disabled
- [ ] "Include the hotbar in Deposit all"
- [ ] The default sort applies when entering a world
- [ ] Chest highlight duration
- [ ] Hide the goal panel, and change how many materials it lists

## 28. Sprites

- [ ] The portable terminal sprite looks right in the inventory and when held

## Also check

- [ ] **Spanish texts:** nothing overlaps (especially the terminal's buttons and the craft row)
- [ ] **The rename:** the mod shows as "Terra Index", and terminals and chests from before still work
