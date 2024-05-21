using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;
using System.Linq;

namespace Converter;

public static class ModManager
{
    public static async Task CreateMod()
    {
        var outsideDescriptor = $@"version=""1.0""
tags={{
	""Total Conversion""
}}
name=""{Settings.Instance.ModName}""
supported_version=""1.12.4""
path=""mod/{Settings.Instance.ModName}""";

        await File.WriteAllTextAsync(Helper.GetPath(Settings.Instance.ModsDirectory, $"{Settings.Instance.ModName}.mod"), outsideDescriptor);

        FileSystem.CopyDirectory(Settings.Instance.TotalConversionSandboxPath, Helper.GetPath(Settings.Instance.ModsDirectory, Settings.Instance.ModName), true);

        var insideDescriptor = $@"version=""1.0""
tags={{
	""Total Conversion""
}}
name=""{Settings.Instance.ModName}""
supported_version=""1.12.4""";
        await File.WriteAllTextAsync(Helper.GetPath(Settings.Instance.ModsDirectory, Settings.Instance.ModName, "descriptor.mod"), insideDescriptor);
    }
    public static bool DoesModExist()
    {
        return Directory.Exists(Helper.GetPath(Settings.Instance.ModsDirectory, Settings.Instance.ModName));
    }

    public static (string? jsonName, string? geojsonName) FindLatestInputs()
    {
        string? jsonName = null;
        string? geojsonName = null;

        var filesToCheck = new DirectoryInfo(SettingsManager.ExecutablePath)
            .EnumerateFiles()
            .OrderByDescending(n => n.CreationTime)
            .Select(n => n.Name)
            .Where(n => Settings.Instance.InputJsonPath != n && Settings.Instance.InputGeojsonPath != n);

        //var filesToCheck = Directory.EnumerateFiles(SettingsManager.ExecutablePath)
        //    .Where(n => Settings.Instance.InputJsonPath != n && Settings.Instance.InputGeojsonPath != n);

        foreach (var f in filesToCheck)
        {
            if (f.EndsWith(".json"))
            {
                var p = Path.GetFileName(f);
                if (!p.EndsWith("settings.json") && !p.StartsWith("ConsoleUI"))
                {
                    jsonName = f;
                }
            }
            else if (f.EndsWith(".geojson"))
            {
                geojsonName = f;
            }

            if (jsonName is not null && geojsonName is not null)
            {
                break;
            }
        }

        return (jsonName, geojsonName);
    }

    private static async Task<Map> LoadMap()
    {
        var geoMap = await MapManager.LoadGeojson();
        var geoMapRivers = new GeoMapRivers([]);
        var jsonMap = await MapManager.LoadJson();
        var map = await MapManager.ConvertMap(geoMap, geoMapRivers, jsonMap);
        map.Settings = Settings.Instance;
        return map;
    }

#if DEBUG
    public static async Task Run()
    {
        int i = 1;
        int totalStageCount = 22;

        var map = await LoadMap();
        Console.WriteLine($"{i++}/{totalStageCount}. Inputs have been loaded.");

        //await MapManager.DrawCells(map);

        //await MapManager.DrawProvinces(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Provinces created.");
        await MapManager.DrawHeightMap(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Heightmap created.");

        var packedHeightmap = await PackedMapManager.CreatePackedHeightMap(map);
        await PackedMapManager.WritePackedHeightMap(packedHeightmap);

        var cells = map.Provinces.Select(n => n.Cells);
        var baronyCells = map.Empires
            .SelectMany(n => n.kingdoms)
            .SelectMany(n => n.duchies)
            .SelectMany(n => n.counties)
            .SelectMany(n => n.baronies)
            .Select(n => n.province);

        await MapManager.DrawRivers(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Rivermap created.");
        await MapManager.WriteDefinition(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Definition created.");

        await MapManager.WriteLocators(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Locators created.");

        var titles = TitleManager.CreateTitles(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Titles created.");
        map.Empires = titles;
        await TitleManager.WriteLandedTitles(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Landed titles created.");
        await TitleManager.WriteTitleLocalization(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Title localization created.");

        var faiths = await MapManager.ApplyCultureReligion(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Culture, Religions created.");

        if (Settings.Instance.OnlyCounts)
        {
            map.Characters = await CharacterManager.CreateCharactersCountOnly(map);
        }
        else
        {
            map.Characters = await CharacterManager.CreateCharacters(map);
        }
        Console.WriteLine($"{i++}/{totalStageCount}. Characters created.");
        await CharacterManager.WriteHistoryCharacters(map);
        Console.WriteLine($"{i++}/{totalStageCount}. History characters created.");
        await CharacterManager.WriteHistoryTitles(map);
        Console.WriteLine($"{i++}/{totalStageCount}. History titles created.");
        await CharacterManager.WriteDynasties(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Dynasties created.");
        await CharacterManager.WriteDynastyLocalization(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Dynasty localization created.");

        await MapManager.WriteHistoryProvinces(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Province history created.");
        await MapManager.CopyOriginalReligions(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Original religions copied.");
        await MapManager.WriteHolySites(map, faiths);
        Console.WriteLine($"{i++}/{totalStageCount}. Holy sites created.");

        await MapManager.WriteDefault(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Default file created.");
        await MapManager.WriteTerrain(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Terrain created.");
        await MapManager.WriteMasks(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Masks created.");

        await MapManager.WriteGraphics();
        Console.WriteLine($"{i++}/{totalStageCount}. Graphics file created.");
    }
#endif
#if RELEASE || PUBLISH
    public static async Task Run()
    {
        int i = 1;
        int totalStageCount = 22;

        var map = await LoadMap();
        Console.WriteLine($"{i++}/{totalStageCount}. Inputs have been loaded.");

        //await MapManager.DrawCells(map);

        await MapManager.DrawProvinces(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Provinces created.");
        await MapManager.DrawHeightMap(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Heightmap created.");
        await MapManager.DrawRivers(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Rivermap created.");
        await MapManager.WriteDefinition(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Definition created.");

        await MapManager.WriteLocators(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Locators created.");

        var titles = TitleManager.CreateTitles(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Titles created.");
        map.Empires = titles;
        await TitleManager.WriteLandedTitles(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Landed titles created.");
        await TitleManager.WriteTitleLocalization(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Title localization created.");

        var faiths = await MapManager.ApplyCultureReligion(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Culture, Religions created.");

        if (Settings.Instance.OnlyCounts)
        {
            map.Characters = await CharacterManager.CreateCharactersCountOnly(map);
        }
        else
        {
            map.Characters = await CharacterManager.CreateCharacters(map);
        }
        Console.WriteLine($"{i++}/{totalStageCount}. Characters created.");
        await CharacterManager.WriteHistoryCharacters(map);
        Console.WriteLine($"{i++}/{totalStageCount}. History characters created.");
        await CharacterManager.WriteHistoryTitles(map);
        Console.WriteLine($"{i++}/{totalStageCount}. History titles created.");
        await CharacterManager.WriteDynasties(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Dynasties created.");
        await CharacterManager.WriteDynastyLocalization(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Dynasty localization created.");

        await MapManager.WriteHistoryProvinces(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Province history created.");
        await MapManager.CopyOriginalReligions(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Original religions copied.");
        await MapManager.WriteHolySites(map, faiths);
        Console.WriteLine($"{i++}/{totalStageCount}. Holy sites created.");

        await MapManager.WriteDefault(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Default file created.");
        await MapManager.WriteTerrain(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Terrain created.");
        await MapManager.WriteMasks(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Masks created.");

        await MapManager.WriteGraphics();
        Console.WriteLine($"{i++}/{totalStageCount}. Graphics file created.");
    }
#endif
}

