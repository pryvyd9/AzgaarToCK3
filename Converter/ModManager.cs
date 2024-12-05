using Microsoft.VisualBasic.FileIO;

namespace Converter;

public static class ModManager
{
    public static async Task CreateMod()
    {
        var outsideDescriptor = CreateDescriptor(true);

        await File.WriteAllTextAsync(Helper.GetPath(Settings.Instance.ModsDirectory, $"{Settings.Instance.ModName}.mod"), outsideDescriptor);

        FileSystem.CopyDirectory(Settings.Instance.TotalConversionSandboxPath, Helper.GetPath(Settings.Instance.ModsDirectory, Settings.Instance.ModName), true);

        var insideDescriptor = CreateDescriptor(false);
        await File.WriteAllTextAsync(Helper.GetPath(Settings.Instance.ModsDirectory, Settings.Instance.ModName, "descriptor.mod"), insideDescriptor);
    }
    public static bool DoesModExist()
    {
        return Directory.Exists(Helper.GetPath(Settings.Instance.ModsDirectory, Settings.Instance.ModName));
    }

    public static string? FindLatestInputs()
    {
        var filesToCheck = new DirectoryInfo(SettingsManager.ExecutablePath)
            .EnumerateFiles()
            .OrderByDescending(n => n.CreationTime)
            .Select(n => n.Name)
            .Where(n => Settings.Instance.InputXmlPath != n);

        return filesToCheck.FirstOrDefault(n => n.EndsWith(".xml"));
    }


    private static async Task<Map> LoadMap()
    {
        var xmlMap = await MapManager.LoadXml();
        var map = await MapManager.ConvertMap(xmlMap);
        map.Settings = Settings.Instance;
        return map;
    }

    private static string CreateDescriptor(bool isOutsideDescriptor)
    {
        const string supportedGameVersion = "1.14.0";

        var descriptor = $@"version=""1.0""
tags={{
	""Total Conversion""
}}
name=""{Settings.Instance.ModName}""
supported_version=""{supportedGameVersion}""";

        if (isOutsideDescriptor)
        {
            descriptor += $@"path=""mod/{Settings.Instance.ModName}""";
        }

        return descriptor;
    }

#if DEBUG
    public static async Task Run()
    {
        int i = 1;
        int totalStageCount = 24;

        var map = await LoadMap();
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Inputs have been loaded.");

        //await MapManager.DrawCells(map);

        //await MapManager.DrawProvinces(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Provinces created.");
        //await HeightMapManager.WriteHeightMap(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Heightmap created.");

        //await MapManager.DrawFlatMap(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Flat map created.");

        //await MapManager.WriteGraphics();
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Graphics file created.");
        //await MapManager.WriteDefines(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Defines file created.");

        //await MapManager.DrawRivers(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Rivermap created.");
        //await MapManager.WriteDefinition(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Definition created.");

        //await MapManager.WriteLocators(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Locators created.");

        //var titles = TitleManager.CreateTitles(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Titles created.");
        //map.Output.Empires = titles;
        //await TitleManager.WriteLandedTitles(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Landed titles created.");
        //await TitleManager.WriteTitleLocalization(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Title localization created.");

        //var faiths = await MapManager.ApplyCultureReligion(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Culture, Religions created.");

        //if (Settings.Instance.OnlyCounts)
        //{
        //    map.Output.Characters = await CharacterManager.CreateCharactersCountOnly(map);
        //}
        //else
        //{
        //    map.Output.Characters = await CharacterManager.CreateCharacters(map);
        //}
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Characters created.");
        //await CharacterManager.WriteHistoryCharacters(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. History characters created.");
        //await CharacterManager.WriteHistoryTitles(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. History titles created.");
        //await CharacterManager.WriteDynasties(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Dynasties created.");
        //await CharacterManager.WriteDynastyLocalization(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Dynasty localization created.");

        //await MapManager.WriteHistoryProvinces(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Province history created.");
        //await MapManager.CopyOriginalReligions(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Original religions copied.");
        //await MapManager.WriteHolySites(map, faiths);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Holy sites created.");

        //await MapManager.WriteDefault(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Default file created.");
        //await MapManager.WriteTerrain(map);
        //MyConsole.WriteLine($"{i++}/{totalStageCount}. Terrain created.");
        await MapManager.WriteMasks(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Masks created.");
    }
#endif
#if RELEASE || PUBLISH
    public static async Task Run()
    {
        int i = 1;
        int totalStageCount = 24;

        var map = await LoadMap();
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Inputs have been loaded.");

        //await MapManager.DrawCells(map);

        await MapManager.DrawProvinces(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Provinces created.");
        await HeightMapManager.WriteHeightMap(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Heightmap created.");

        await MapManager.DrawFlatMap(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Flat map created.");

        await MapManager.WriteGraphics();
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Graphics file created.");
        await MapManager.WriteDefines(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Defines file created.");

        await MapManager.DrawRivers(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Rivermap created.");
        await MapManager.WriteDefinition(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Definition created.");

        await MapManager.WriteLocators(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Locators created.");

        var titles = TitleManager.CreateTitles(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Titles created.");
        map.Output.Empires = titles;
        await TitleManager.WriteLandedTitles(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Landed titles created.");
        await TitleManager.WriteTitleLocalization(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Title localization created.");

        var faiths = await MapManager.ApplyCultureReligion(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Culture, Religions created.");

        if (Settings.Instance.OnlyCounts)
        {
            map.Output.Characters = await CharacterManager.CreateCharactersCountOnly(map);
        }
        else
        {
            map.Output.Characters = await CharacterManager.CreateCharacters(map);
        }
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Characters created.");
        await CharacterManager.WriteHistoryCharacters(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. History characters created.");
        await CharacterManager.WriteHistoryTitles(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. History titles created.");
        await CharacterManager.WriteDynasties(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Dynasties created.");
        await CharacterManager.WriteDynastyLocalization(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Dynasty localization created.");

        await MapManager.WriteHistoryProvinces(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Province history created.");
        await MapManager.CopyOriginalReligions(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Original religions copied.");
        await MapManager.WriteHolySites(map, faiths);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Holy sites created.");

        await MapManager.WriteDefault(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Default file created.");
        await MapManager.WriteTerrain(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Terrain created.");
        await MapManager.WriteMasks(map);
        MyConsole.WriteLine($"{i++}/{totalStageCount}. Masks created.");
    }
#endif
}

