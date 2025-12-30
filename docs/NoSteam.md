If you run the game not in Steam you will see the error that the directories were not found.
Create a file named `settings.json` and put this inside:
```
{
  "ModsDirectory": "F:\\Projects\\Paradox Interactive\\Crusader Kings III\\mod",
  "Ck3Directory": "G:\\Games\\SteamLibrary\\steamapps\\common\\Crusader Kings III\\game",
  "TotalConversionSandboxPath": "G:\\Games\\SteamLibrary\\steamapps\\workshop\\content\\1158310\\3595862458",
  "InputXmlPath": "Chaia 2024-12-06-23-43.xml",
  "ModName": "PublishMod",
  "ShouldOverride": false,
  "OnlyCounts": false,
  "MapWidth": 8192,
  "MapHeight": 4096,
  "MaxThreads": 16,
  "HeightMapBlurStdDeviation": 15,
}
```

Change values in `ModsDirectory`, `Ck3Directory`, `TotalConversionSandboxPath` to ones that suit you based on your system.
