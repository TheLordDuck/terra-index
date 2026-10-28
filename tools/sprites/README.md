# Sprite tools

The Apothecary's sprite is built on the Guide's vanilla sheet, so it has his poses and 26-frame animation.

1. Extract the Guide's texture (`xnb` reads the game's compressed `.xnb` files with tModLoader's FNA.dll):
   `dotnet run --project xnb -- <Terraria>/Content/Images/NPC_22.xnb NPC_22.rgba`
2. In the same folder, run `node apothecary.js`: it writes `gen/Apothecary.rgba` and `gen/Apothecary_Head.rgba`.
3. `powershell -File topng.ps1 gen` turns them into PNGs; copy those to `TerraIndex/Content/NPCs/`.
   `sheet.ps1` lays a sheet out scaled up, with frame numbers, to look at it.

The extracted vanilla files are not kept in the repo.
