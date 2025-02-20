using Microsoft.VisualBasic.FileIO;

namespace Converter;

public static class ModManager
{
    private record Context(Map map, string[] faiths);

    private class StepNode
    {
        protected readonly string name;
        protected readonly Func<Context, Task>? func;
        protected readonly Func<Context, Task<Context>>? funcMutable;
        protected readonly StepNode[]? children;

        public StepNode(string name, Func<Context, Task> func) => (this.name, this.func) = (name, func);
        public StepNode(string name, Func<Context, Task<Context>> funcMutable) => (this.name, this.funcMutable) = (name, funcMutable);
        public StepNode(string name, StepNode[] children) => (this.name, this.children) = (name, children);

        public virtual async Task<Context> Run(Context context, int outerStepCount = 0, params int[] stepIndexes)
        {
            if (func is not null)
            {
                await func.Invoke(context);
            }
            if (funcMutable is not null)
            {
                context = await funcMutable.Invoke(context);
            }
            else if (children is not null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    context = await children[i].Run(context, children.Length, [.. stepIndexes, i + 1]);
                }
            }

            if (stepIndexes?.Length > 0)
            {
                MyConsole.Info($"{GetIndexString([.. stepIndexes])}/{outerStepCount}. {name}");
            }
            else
            {
                MyConsole.Info(name);
            }

            return context;
        }

        protected static string GetIndexString(params int[] stepIs)
        {
            return string.Join(".", stepIs);
        }
    }

    private class StepNodeParallel : StepNode
    {
        public StepNodeParallel(string name, Func<Context, Task> func) : base(name, func) { }
        public StepNodeParallel(string name, StepNode[] children) : base(name, children) { }

        public override async Task<Context> Run(Context context, int outerStepCount = 0, params int[] stepIndexes)
        {
            using SemaphoreSlim semaphore = new(Settings.Instance.MaxThreads);

            MyConsole.Info($"{GetIndexString([.. stepIndexes])}/{outerStepCount}: Starting parallel processing...");

            if (children?.Length > 0)
            {
                var tasks = Enumerable.Range(0, children.Length).Select(async i =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        if (Settings.Instance.MaxThreads > 1)
                        {
                            await Task.Yield();
                        }
                        await children[i].Run(context, children.Length, [.. stepIndexes, i + 1]);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }).ToArray();

                await Task.WhenAll(tasks);
            }

            if (stepIndexes?.Length > 0)
            {
                MyConsole.Info($"{GetIndexString([.. stepIndexes])}/{outerStepCount}. {name}");
            }
            else
            {
                MyConsole.Info(name);
            }

            return context;
        }
    }

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
        var inputMap = await MainConverter.LoadMap();
        var xmlMap = await MainConverter.LoadXml();
        var map = await MainConverter.ConvertMap(xmlMap, inputMap);
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
        StepNode steps = new("Conversion finished.", [
            new ("Inputs have been loaded.", async c => c with { map = await LoadMap() }),
            //new ("Cell map have been drawn.", c => MapManager.DrawCells(c.map)),
            //new ("Provinces created.", c => MainConverter.DrawProvinces(c.map)),
            new StepNodeParallel("Finished order independent steps.", [
                new ("Heightmap created.", c => HeightMapConverter.WriteHeightMap(c.map)),
                //new ("Flat map created.", c => MainConverter.DrawFlatMap(c.map)),
                //new ("Graphics file created.", _ => MainConverter.WriteGraphics()),
                //new ("Defines file created.", c => MainConverter.WriteDefines(c.map)),
                //new ("Rivermap created.", c => MainConverter.DrawRivers(c.map)),
                //new ("Masks created.", c => BiomeConverter.WriteMasks(c.map)),
                //new ("Pdxterrain created.", c => MainConverter.WritePdxterrain()),
            ]),
            //new ("Definition created.", c => MainConverter.WriteDefinition(c.map)),
            //new ("Locators created.", c => MainConverter.WriteLocators(c.map)),
            //new ("Titles created.", async c => c.map.Output.Empires = TitleManager.CreateTitles(c.map)),
            //new ("Landed titles created.", c => TitleManager.WriteLandedTitles(c.map)),
            //new ("Title localization created.", c => TitleManager.WriteTitleLocalization(c.map)),
            //new ("Culture, Religions created.", async c => c with { faiths = await MainConverter.ApplyCultureReligion(c.map) }),
            //new ("History provinces created.", c => MainConverter.WriteHistoryProvinces(c.map)),
            //new ("Characters created.", async c => c.map.Output.Characters = Settings.Instance.OnlyCounts
            //    ? await CharacterManager.CreateCharactersCountOnly(c.map)
            //    : await CharacterManager.CreateCharacters(c.map)),
            //new ("History characters created.", c => CharacterManager.WriteHistoryCharacters(c.map)),
            //new ("History titles created.", c => CharacterManager.WriteHistoryTitles(c.map)),
            //new ("Dynasties created.", c => CharacterManager.WriteDynasties(c.map)),
            //new ("Dynasty localization created.", c => CharacterManager.WriteDynastyLocalization(c.map)),
            //new ("Original religions copied.", c => MainConverter.CopyOriginalReligions(c.map)),
            //new ("Holy sites created.", c => MainConverter.WriteHolySites(c.map, c.faiths)),
            //new ("Default file created.", c => MainConverter.WriteDefault(c.map)),
            //new ("Terrain created.", c => MainConverter.WriteTerrain(c.map)),
        ]);

        await steps.Run(new Context(null!, null!));
    }
#endif
#if RELEASE || PUBLISH
    public static async Task Run()
    {
        StepNode steps = new("Conversion finished.", [
            new ("Inputs have been loaded.", async c => c with { map = await LoadMap() }),
            //new ("Cell map have been drawn.", c => MainConverter.DrawCells(c.map)),
            new ("Provinces created.", c => MainConverter.DrawProvinces(c.map)),
            new StepNodeParallel("Finished order independent steps.", [
                new ("Heightmap created.", c => HeightMapConverter.WriteHeightMap(c.map)),
                new ("Flat map created.", c => MainConverter.DrawFlatMap(c.map)),
                new ("Graphics file created.", _ => MainConverter.WriteGraphics()),
                new ("Defines file created.", c => MainConverter.WriteDefines(c.map)),
                new ("Rivermap created.", c => MainConverter.DrawRivers(c.map)),
                new ("Masks created.", c => BiomeConverter.WriteMasks(c.map)),
                new ("Pdxterrain created.", c => MainConverter.WritePdxterrain()),
            ]),
            new ("Definition created.", c => MainConverter.WriteDefinition(c.map)),
            new ("Locators created.", c => MainConverter.WriteLocators(c.map)),
            new ("Titles created.", async c => c.map.Output.Empires = TitleManager.CreateTitles(c.map)),
            new ("Landed titles created.", c => TitleManager.WriteLandedTitles(c.map)),
            new ("Title localization created.", c => TitleManager.WriteTitleLocalization(c.map)),
            new ("Culture, Religions created.", async c => c with { faiths = await MainConverter.ApplyCultureReligion(c.map) }),
            new ("History provinces created.", c => MainConverter.WriteHistoryProvinces(c.map)),
            new ("Characters created.", async c => c.map.Output.Characters = Settings.Instance.OnlyCounts
                ? await CharacterManager.CreateCharactersCountOnly(c.map)
                : await CharacterManager.CreateCharacters(c.map)),
            new ("History characters created.", c => CharacterManager.WriteHistoryCharacters(c.map)),
            new ("History titles created.", c => CharacterManager.WriteHistoryTitles(c.map)),
            new ("Dynasties created.", c => CharacterManager.WriteDynasties(c.map)),
            new ("Dynasty localization created.", c => CharacterManager.WriteDynastyLocalization(c.map)),
            new ("Original religions copied.", c => MainConverter.CopyOriginalReligions(c.map)),
            new ("Holy sites created.", c => MainConverter.WriteHolySites(c.map, c.faiths)),
            new ("Default file created.", c => MainConverter.WriteDefault(c.map)),
            new ("Terrain created.", c => MainConverter.WriteTerrain(c.map)),
        ]);

        await steps.Run(new Context(null!, null!));
    }
#endif
}

