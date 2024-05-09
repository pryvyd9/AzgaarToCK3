using Microsoft.VisualBasic.FileIO;

namespace Converter;

public static class ModManager
{
    public static async Task CreateMod()
    {
        var outsideDescriptor = $@"version=""1.0""
tags={{
	""Total Conversion""
}}
name=""{SettingsManager.Settings.modName}""
supported_version=""1.12.4""
path=""mod/{SettingsManager.Settings.modName}""";

        await File.WriteAllTextAsync($"{SettingsManager.Settings.modsDirectory}/{SettingsManager.Settings.modName}.mod", outsideDescriptor);

        FileSystem.CopyDirectory(SettingsManager.Settings.totalConversionSandboxPath, $"{SettingsManager.Settings.modsDirectory}/{SettingsManager.Settings.modName}", true);

        var insideDescriptor = $@"version=""1.0""
tags={{
	""Total Conversion""
}}
name=""{SettingsManager.Settings.modName}""
supported_version=""1.12.4""";
        await File.WriteAllTextAsync($"{SettingsManager.Settings.modsDirectory}/{SettingsManager.Settings.modName}/descriptor.mod", insideDescriptor);
    }
    public static bool DoesModExist()
    {
        return Directory.Exists($"{SettingsManager.Settings.modsDirectory}/{SettingsManager.Settings.modName}");
    }


    private static async Task<Map> LoadMap()
    {
        var geoMap = await MapManager.LoadGeojson();
        var geoMapRivers = new GeoMapRivers(Array.Empty<FeatureRivers>());
        var jsonMap = await MapManager.LoadJson();
        var map = await MapManager.ConvertMap(geoMap, geoMapRivers, jsonMap);
        map.Settings = SettingsManager.Settings;
        return map;
    }

#if DEBUG
    public static async Task Run()
    {
        int i = 1;
        int totalStageCount = 16;

        var map = await LoadMap();
        Console.WriteLine($"{i++}/{totalStageCount}. Inputs have been loaded.");

        await MapManager.DrawCells(map);

        await MapManager.DrawProvinces(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Provinces created.");
        //await MapManager.DrawHeightMap(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Heightmap created.");
        //await MapManager.DrawRivers(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Rivermap created.");
        //await MapManager.WriteDefinition(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Definition created.");

        //await MapManager.WriteLocators(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Locators created.");

        //var titles = MapManager.CreateTitles(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Titles created.");
        //map.Empires = titles;
        //await MapManager.WriteLandedTitles(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Landed titles created.");
        //await MapManager.WriteTitleLocalization(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Title localization created.");

        //var faiths = await MapManager.WriteHistoryProvinces(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Province history created.");
        //await MapManager.CopyOriginalReligions(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Original religions copied.");
        //await MapManager.WriteHolySites(map, faiths);
        //Console.WriteLine($"{i++}/{totalStageCount}. Holy sites created.");

        //await MapManager.WriteDefault(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Default file created.");
        //await MapManager.WriteTerrain(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Terrain created.");
        //await MapManager.WriteMasks(map);
        //Console.WriteLine($"{i++}/{totalStageCount}. Masks created.");

        //await MapManager.WriteGraphics();
        //Console.WriteLine($"{i++}/{totalStageCount}. Graphics file created.");

        Environment.Exit(0);
    }
#endif
#if RELEASE
    public static async Task Run()
    {
        int i = 1;
        int totalStageCount = 16;

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

        var titles = MapManager.CreateTitles(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Titles created.");
        map.Empires = titles;
        await MapManager.WriteLandedTitles(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Landed titles created.");
        await MapManager.WriteTitleLocalization(map);
        Console.WriteLine($"{i++}/{totalStageCount}. Title localization created.");

        var faiths = await MapManager.WriteHistoryProvinces(map);
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

