# Sprite tools

The Apothecary's sprite is built on the Guide's vanilla sheet, so it has his poses and 26-frame animation.

1. Extract the Guide's texture (`xnb` reads the game's compressed `.xnb` files with tModLoader's FNA.dll):
   `dotnet run --project xnb -- <Terraria>/Content/Images/NPC_22.xnb NPC_22.rgba`
2. In the same folder, run `node apothecary.js`: it writes `gen/Apothecary.rgba` and `gen/Apothecary_Head.rgba`.
3. `powershell -File topng.ps1 gen` turns them into PNGs; copy those to `TerraIndex/Content/NPCs/`.
   `sheet.ps1` lays a sheet out scaled up, with frame numbers, to look at it.

The extracted vanilla files are not kept in the repo.

4. From the repo root, `python tools/sprites/terrarian.py` repaints that hooded sheet as "The Terrarian" (the user's
   character): brown hair with red bangs, a gold band with blue, green and red plumes, a navy coat and white
   trousers. It overwrites both PNGs, so run it once, on step 3's output.

## Mod icon

From the repo root, `python tools/sprites/icon.py` redraws `TerraIndex/icon.png` and `icon_small.png` from the mod's own
sprites (portable terminal, wireless teleporter) plus a drawn terminal and Calamity guide book. Rerun it after
changing any of those sprites.
