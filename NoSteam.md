If you run the game not in Steam you will see the error that the directories were not found.
Create a file named `settings.json` and put this inside:
```
{
  "ModsDirectory": "F:\\Projects\\Paradox Interactive\\Crusader Kings III\\mod",
  "Ck3Directory": "G:\\Games\\SteamLibrary\\steamapps\\common\\Crusader Kings III\\game",
  "TotalConversionSandboxPath": "G:\\Games\\SteamLibrary\\steamapps\\workshop\\content\\1158310\\3337607192",
  "InputJsonPath": "Chilesia_Full_2024-06-02-23-04.json",
  "InputGeojsonPath": "Chilesia_Cells_2024-06-02-23-04.geojson",
  "ModName": "NewMod",
  "ShouldOverride": null,
  "OnlyCounts": false
}
```

Change values in `ModsDirectory`, `Ck3Directory`, `TotalConversionSandboxPath` to ones that suit you based on your system.