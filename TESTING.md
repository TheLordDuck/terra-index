# Testing checklist (version 0.8)

0.4 compiles since 2026-10-22 (one type error fixed), but none of 0.4 to 0.7 has been tested in game yet. 0.7 was
written without a compiler: build it first and copy any errors.
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

## Taking items out of the terminal

- [ ] Click an item: a full stack (or all there is) comes out of the door, with no pop-up
- [ ] Right click an item: it appears on the cursor with 1; each right click adds 1
- [ ] Hold right click: the amount goes up slowly, then faster and faster, and stops at what is stored / one stack
- [ ] With an amount on the cursor, click any item: exactly that amount comes out
- [ ] Right click empty space in the panel: the cursor item disappears and the hint goes back to normal
- [ ] Right click a different item: the selection switches to it, starting at 1
- [ ] Ctrl + click shows the item's recipe in the index next to the terminal (the terminal stays open); Shift + click still highlights the chests
- [ ] Both hint lines fit in the panel (English and Spanish)

## 35. Index next to the terminal

- [ ] The terminal's **Recipes** button opens the index on its left; both are centered together and don't overlap
- [ ] Recipes, Tree, Craft and Take missing work in the index while the terminal stays open, and the terminal's list updates after crafting
- [ ] Typing in one search box doesn't type in the other; clicking one box stops typing in the other
- [ ] Taking an item out (click or picked amount) keeps both open; the list shows the new amount right away
- [ ] "Deposit all" keeps the terminal open
- [ ] Closing the terminal and opening it again brings the index back
- [ ] Closing the index with its X (or N) while the terminal is open: next time the terminal opens alone
- [ ] Esc closes both; walking away closes both
- [ ] "Deposit items" closes the index (the inventory takes its place)
- [ ] With the terminal closed, the index opens centered as before
- [ ] At a smaller resolution or a bigger UI scale, the index gets narrower instead of covering the terminal

## 35. Goal "next step" button

- [ ] Set an Iron Broadsword as the goal with only Iron Ore: near a Furnace the panel shows "Craft N x Iron Bar"; clicking it turns the ore into bars
- [ ] Then, near an Anvil, it shows "Craft 1 x Iron Broadsword", and clicking it gives the sword (with a prefix)
- [ ] Away from the station, the row says which station the next step needs
- [ ] Recipe groups ("Any Wood") use any wood you have
- [ ] Near a terminal whose chests hold the ingredients (not your inventory), the button says "(chests)" and the item comes out of the door
- [ ] A fast double click crafts only once; the button is grey for a moment
- [ ] Folded, the button is hidden; the panel is still hidden with the inventory open
- [ ] Multiplayer: the other player sees your inventory change and the crafted item

## 38 and 39. Delivery drones and chest effects

- [ ] Taking an item out: the chest sparks, a drone flies from it to the door carrying the item, the door opens as it arrives and the item drops
- [ ] Deposit all / Deposit items: the items fly into the door, then drones carry them to the chests, which spark when they arrive
- [ ] Crafting from the chests: drones bring the ingredients from their chests to the door
- [ ] The drone faces the way it flies, its rotors spin, and it can be seen in the dark
- [ ] Turning off "Delivery drones" or "Chest effects" in the settings removes each one
- [ ] With the portable terminal far away: no drones, items still appear next to you
- [ ] Multiplayer: the other player sees the drones and sparks too

## Comfort

- [ ] Standing at the terminal's door, a taken item drops out of the door as before
- [ ] A few blocks away (panel still open), the drone flies from the chest to you and the item goes into your inventory; the door doesn't open
- [ ] Walking while the drone flies: it follows you
- [ ] Inventory full: the item lands at your feet, not lost
- [ ] "Take missing" and middle click also bring the items to you
- [ ] With the portable terminal far away, items still appear next to you at once
- [ ] Turning off "Type in the terminal's search box when it opens": N, [ and ] work right after opening the terminal
- [ ] Search "wood", close the terminal, reopen it within 30 s: the search, tab and page are still there; after 30 s, or at another terminal, it is empty
- [ ] Index open near the terminal: middle click an item in the grid and an ingredient in a recipe takes a stack out
- [ ] Middle click with no terminal in reach, or on an item that isn't stored: a message explains why
- [ ] Multiplayer: the other player sees the drone flying to you

## Blueprints (0.8)

- [ ] Craft the Blueprint (10 wood at a work bench); its tooltip explains it
- [ ] Right click: the catalog opens with 4 houses; the material list starts with Wood and includes other woods and glass (and modded woods if you have any)
- [ ] Changing house or material updates the small preview and the cost
- [ ] Cost colors: green when you have it; blue when the rest is in the terminal's chests; red when missing
- [ ] Take missing (near a terminal) brings the missing materials, and the lines turn green
- [ ] Place closes the catalog; holding the item shows the see-through house standing on the tile under the mouse
- [ ] Over dirt or stone the cells turn red with a message; over grass and plants it stays green
- [ ] Left click builds it: walls, blocks, platforms, door, chairs, tables and torches all in place; smoke and a sound; the materials leave your inventory
- [ ] Missing materials: a message lists them and nothing is built
- [ ] Every room of the small house, the two-floor house and the tower is valid housing (housing query / an NPC moves in)
- [ ] Boss arena: three rows of platforms you can jump between
- [ ] Building over existing background walls keeps them and costs fewer walls
- [ ] With another material (e.g. Boreal Wood or Glass) everything uses that material
- [ ] Multiplayer: the other player sees the house; building where something is in the way gives the materials back
- [ ] Medieval house, castle, pagoda and wizard tower look like `docs/blueprints/` (roof slopes the right way round: if they are mirrored, swap the slopes in `Blueprints.TileBrushes`)
- [ ] Their fixed materials show up in the cost (gray brick, red brick, glass walls, shingles...) and whatever isn't in this game is simply left out
- [ ] Furniture the wood you picked doesn't have (e.g. a clock) uses plain wood's or a vanilla one; lights never go missing
- [ ] Every NPC room of the big ones is valid housing (medieval 5, castle 11, pagoda 3, wizard tower 4)
- [ ] The catalog fits on screen with the castle selected (cost in two columns)

## 40 and 41. Languages and help

- [ ] Switch the game to each language (German, French, Portuguese, Russian, Italian, Polish, Chinese): the panels show translated texts, not keys
- [ ] Nothing overlaps in the terminal's header (the ? button, Deposit all, Deposit items) and the Recipes / Sort row, especially in German and Russian
- [ ] Hovering **?** in the terminal and in the index shows the list of controls, fully on screen
- [ ] New character: placing the first terminal shows the wiring tip (with the Red Wrench's and Mechanic's names in your language); opening it shows the help tip. Neither shows again for that character

## Radio

- [ ] Craft it at an anvil (8 Wood, 3 Iron/Lead Bars, 2 Glass) and place it on the floor: same size as a Piggy Bank
- [ ] A few seconds after placing it, one of the two clips plays; the next time, the other one
- [ ] While a clip plays, music notes float out by the antenna (and stop when it ends or is turned off)
- [ ] The gaps between clips change (sometimes ~10 s, sometimes a minute or two)
- [ ] Right click: the dial and the light go dark and it stops (also in the middle of a clip); right click again turns it on
- [ ] Walking away makes it quieter; two radios next to each other don't play at the same time
- [ ] Breaking it (on or off) drops the radio
- [ ] Multiplayer: turning it on/off is seen by the other player, and both players hear the same clip at the same time

## Performance

- [ ] With a big storage (lots of chests and item types), the terminal panel doesn't hitch every half second
- [ ] Changes made by another player still show up in an open terminal within a second (multiplayer)
- [ ] Opening a terminal right after closing the recipe index shows its items (not an empty list)
- [ ] The goal panel still updates when you pick up or use a material

## 28. Sprites

- [ ] The portable terminal sprite looks right in the inventory and when held

## Also check

- [ ] **Spanish texts:** nothing overlaps (especially the terminal's buttons and the craft row)
- [ ] **The rename:** the mod shows as "Terra Index", and terminals and chests from before still work
