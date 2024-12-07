# Azgaar's Fantasy Map Generator to Crusader Kings III
## Installation
- Download the latest release and extract it to any folder
- Subscribe to [地圖沙盒(1.13)/Total Conversion Sandbox(1.13)](https://steamcommunity.com/sharedfiles/filedetails/?id=3337607192) mod on the workshop.
(You do not need to add it to playset. It is only required for map conversion process)

Supported game version: `1.14.0+`
Discord: https://discord.gg/CqHcpRRH

## Generation requirements
- use `Provinces ratio` = 100.
- do not use high `Points number` option. Heightmap will be smoothed down and the borders will not match terrain.

## Generation recommendations
- Set `Cultures number` > `Religions number`.
- Use high `States number`, `Towns number`

## Usage
1. Generate a map via https://pryvyd9.github.io/Fantasy-Map-Generator/ (It is a special version for conversion). You can load your existing Azgaar map there and then export it.
2. Export Crusader Kings 3
![screenshot](docs/Screenshot_2024-11-22_190012.png)
3. Place this file in the extracted folder
4. Run `ConsoleUI` file
5. Follow the instructions
6. Launch the game making sure the newly created mod is added to the playset and enabled
7. Create your own ruler

### Optional steps
Do them if there are issues with holding/unit placement or terrain looks weird. Or it crashes.

[Map editor guide](https://github.com/pryvyd9/AzgaarToCK3/blob/master/docs/MapEditor.md/)

## More Usage
- You can delete the `settings.json` to reconfigure everything or edit `settings.json` to suit your needs.
- Set `onlyCounts` = true to make all characters start as counts.
- [No Steam or directory not found](https://github.com/pryvyd9/AzgaarToCK3/blob/master/docs/NoSteam.md/)
- Change MaxThreads value to 1 if you wish to limit CPU and RAM usage. Use default value (= cpu core count) for max performance.
- You can change map size with `MapWidth` and `MapHeight`. But make sure they are divisible by 32.

## Quirks
- Azgaar only has 2 titles and CK3 has 5:
	- barony = province
	- county = 4 neighboring provinces of the same state
	- duchy = state
	- kingdom = all duchies of the same dominant culture
	- empire = all kingdoms of the same dominant religion
- Biome to terrain conversion is complicated and is WIP.
- Detached single cell parts of provinces are reassigned to neighboring provinces.
- Single cell provinces are deleted as it is not possible to put locators inside due to small size.
- Dynasties are randomized based on basenames. They can repeat.
- No heads of religion.
- Holy sites are mapped to random provinces/counties.
- Characters are created and assigned titles randomly. They may have too many domains which they will give out after unpausing.
- Cultures are mapped to random existing cultures.
- Religions are mapped to random existing religions.
- Rivers are not generated.
- Biomes do not have smooth transition. They are still WIP. [Biome Conversion](https://github.com/pryvyd9/AzgaarToCK3/blob/master/Converter/Helper.cs#L158-L169)
- Map ratios are not preserved. If you create a square map in Azgaar you will get it streched. To fix it change `MapWidth` and `MapHeight` to match your ratios but make sure they are divisible by 32.

## Known issues
- Map painting is not perfect.

## Multiplayer
- use [[UMMS]Ultimate Modded Multiplayer Solver:null checksum](https://steamcommunity.com/sharedfiles/filedetails/?id=3227254722) mod
- use [IronyModManager](https://bcssov.github.io/IronyModManager/) to export the playset with the custom mod and friends should import the exported file