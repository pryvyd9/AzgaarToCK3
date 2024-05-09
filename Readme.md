# Azgaar's Fantasy Map Generator to Crusaider Kings III
## Installation
- Download latest release and extract it to any folder
- Subscribe to [Total Conversion Sandbox](https://steamcommunity.com/sharedfiles/filedetails/?id=2524797018) mod on workshop

## Quirks
- Azgaar only has 2 titles and CK3 has 5:
	- barony = province
	- county = 4 neighboring provinces of the same state
	- duchy = state
	- kingdom = all duchies of the same dominant culture
	- empire = all kingdoms of the same dominant religion
- Biome to terrain conversion is complicated and is WIP.
- Detached single cell parts of provinces are reassigned to neighboring province.
- Single cell provinces are deleted as it is not possible to put locators inside due to small size.
- Characters are not generated yet to everyone starts as counts. Use [Entitled](https://steamcommunity.com/sharedfiles/filedetails/?id=2984126808) mod to as a workaround for now.
- No heads of religion.
- Holy sites are mapped to random provinces/counties.
- Cultures are mapped to random existing cultures.
- Religions are mapped to random existing religions.
- Rivers are not generated.

## Known issues
- Water provinces are rarely convex. It means that ship routes will look like navigators are all drunk.
- Map painting is not perfect.

## Multiplayer
- use [[UMMS]Ultimate Modded Multiplayer Solver:null checksum](https://steamcommunity.com/sharedfiles/filedetails/?id=3227254722) mod
- use [IronyModManager](https://bcssov.github.io/IronyModManager/) to export playset with custom mod and friends should import the exported file

## Usage
1. Generate a map via https://azgaar.github.io/Fantasy-Map-Generator/
2. Export GeoJSON cells and JSON full
![screenshot](docs/photo_2024-05-08_21-40-06.jpg)
3. Place these files in the extracted folder
4. Run the .exe file
5. Follow the instructions
6. Go to properties of CK3 in Steam and add `-mapeditor` parameter
7. Launch the game making sure the newly created mod is added to the playset and enabled
8. Repack heightmap
![screenshot](docs/Screenshot_2024-05-08_214628.png)
10. Go to Map Objects Editor. Click on any territory in the list and select all with Ctrl+A hotkey
![screenshot](docs/Screenshot_2024-05-08_214847.png)
11. Click on `Automatically place...` button
![screenshot](docs/Screenshot_2024-05-08_215322.png)
12. Some of the territories failed to add locators properly. Click on Filter all entries that contain errors
![screenshot](docs/Screenshot_2024-05-08_215116.png)
13. Repeat 10-12 until there are no entries with errors
14. If some entries won't fix themselves select the entry, check what object fails and click on `Configure Autonudge...` button.
![screenshot](docs/Screenshot_2024-05-08_215624.png)
Then find settings related to that type of object and tweak them then retry 10-12.
Usually changing some distance parameters helps.
If that still did not help then select the object and move it by hand.
Hopefully there not many of them.
15. Make any other changes in map editor.
16. Save all and exit (Alt+F4 if it restarts the game instead of exit)
![screenshot](docs/Screenshot_2024-05-08_220216.png)
17. Remove -mapeditor launch option and run the game
18. Enjoy!

## More Usage
- You can delete the `settings.json` to reconfigure everything or edit `settings.json` to suit your needs

### Thanks
- [flinker](https://www.youtube.com/@flinkerCK) for helpful mod editing tutorials
- [Azgaar](https://github.com/Azgaar) for help with integration