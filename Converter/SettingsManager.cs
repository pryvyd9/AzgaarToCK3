using System.Text.Json;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Converter;

public class Settings
{
    public required string ModsDirectory { get; init; }
    public required string Ck3Directory { get; init; }
    public required string TotalConversionSandboxPath { get; init; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public string InputJsonPath { get; set; }
    public string InputGeojsonPath { get; set; }
    public string ModName { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public bool? ShouldOverride { get; set; } = null;
    public bool OnlyCounts { get; set; } = false;

    [JsonIgnore]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public static Settings Instance { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [JsonIgnore]
    public static string OutputDirectory => Helper.GetPath(Instance.ModsDirectory, Instance.ModName);

    public bool Debug { get; set; } = true;

    // This is based on guestimate observations form CK3
    // Sparsley populated areas often have fewer baronies per county than densely populated areas

    /// <summary>
    /// High population threshold. If a duchy's total population exceeds this value,
    /// the <see cref="MinCounties"/> is used, resulting in fewer but larger counties.
    /// </summary>
    /// <value>The high population threshold, default is 13000.</value>
    public int HighPopulationThreshold { get; set; } = 13000;

    /// <summary>
    /// Low population threshold. If a duchy's total population is below this value,
    /// the <see cref="MaxCounties"/> is used, resulting in smaller counties with fewer baronies.
    /// </summary>
    /// <value>The low population threshold, default is 2000.</value>
    public int LowPopulationThreshold { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the minimum number of counties. This is the minimum division of baronies within a duchy,
    /// used when the duchy's population exceeds the high population threshold.
    /// </summary>
    /// <value>The minimum number of counties, default is 2.</value>
    public int MinCounties { get; set; } = 2;

    /// <summary>
    /// Gets or sets the maximum number of counties. This is the maximum division of baronies within a duchy,
    /// used when the duchy's population is below the low population threshold.
    /// </summary>
    /// <value>The maximum number of counties, default is 5.</value>
    public int MaxCounties { get; set; } = 5;
    /// <summary>
    /// If true then de jure empires will form based on culture.
    /// If false then de jure empires will form based on religion.
    /// </summary>
    public bool EmpireFromCulture { get;  set; } = true;
    /// <summary>
    /// The minimum number of duchies per kingdom. 
    /// Kingdoms below this number will be merged into adjacent larger kingdoms in same empire.
    /// </summary>
    public int MinimumDuchiesPerKingdom { get;  set; } = 4;
    /// <summary>
    /// The minimum number of kingdoms per empire.
    /// Empires below this number will be merged into adjacent larger empires.
    /// </summary>
    public int MinimumKingdomsPerEmpire { get; set; } = 2;

    public override string ToString()
    {
        var lines = new List<string>();
        foreach (var property in Instance.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            lines.Add($"{property.Name,-30}: {property.GetValue(Instance)}");
        }

        return string.Join('\n', lines);
    }
}

[JsonSerializable(typeof(Settings))]
[JsonSourceGenerationOptions(WriteIndented = true, AllowTrailingCommas = true, PropertyNameCaseInsensitive = true)]
public partial class SettingsJsonContext : JsonSerializerContext { }

public static class SettingsManager
{
    private static readonly string settingsFileName = Helper.GetPath(ExecutablePath, "settings.json");
    private static readonly string defaultModsDirectory = Helper.GetPath(MyDocuments, "Paradox Interactive", "Crusader Kings III", "mod");
    private static string MyDocuments => Helper.GetPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    public static string ExecutablePath => Helper.GetPath(Directory.GetParent(Environment.ProcessPath!)!.FullName);

    private static string GetSteamLibraryFoldersPath()
    {
        if (!Environment.Is64BitOperatingSystem)
        {
            throw new Exception("Only x64 systems are supported.");
        }

        if (OperatingSystem.IsWindows())
        {
            var steamPath = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam")
                ?.GetValue("InstallPath") as string
                ?? throw new Exception("Could not find steam InstallPath");

            return Helper.GetPath(steamPath, "steamapps", "libraryfolders.vdf");
        }
        else if (OperatingSystem.IsMacOS())
        {
            var steamPath = Helper.GetPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Steam");
            return Helper.GetPath(steamPath, "steamapps", "libraryfolders.vdf");
        }
        else
        {
            throw new Exception("Operating System not supported");
        }
    }

    private static string GetGameDirectory()
    {
        var libraries = File.ReadAllText(GetSteamLibraryFoldersPath());
        var pathRegex = new Regex("\"path\"\\s*\"(.+)\"");
        var paths = pathRegex.Matches(libraries).Select(n => n.Groups[1].Value);

        var ck3Directories = paths.Select(n => Helper.GetPath(n, "steamapps", "common", "Crusader Kings III", "game")).Where(Directory.Exists).ToArray();
        if (ck3Directories.Length > 1)
        {
            Debugger.Break();
            throw new Exception("Multiple game directories found.");
        }
        else if (ck3Directories.Length == 0)
        {
            Debugger.Break();
            throw new Exception("No game directories found.");
        }

        return ck3Directories[0];
    }
    private static string GetTotalConversionSandboxDirectory()
    {
        var libraries = File.ReadAllText(GetSteamLibraryFoldersPath());
        var pathRegex = new Regex("\"path\"\\s*\"(.+)\"");
        var paths = pathRegex.Matches(libraries).Select(n => n.Groups[1].Value);

        var ck3Directories = paths.Select(n => Helper.GetPath(n, "steamapps", "workshop", "content", "1158310", "2524797018")).Where(Directory.Exists).ToArray();
        if (ck3Directories.Length > 1)
        {
            Debugger.Break();
            throw new Exception("Multiple mod directories found.");
        }
        else if (ck3Directories.Length == 0)
        {
            Debugger.Break();
            throw new Exception("No mod directories found.");
        }

        return ck3Directories[0];
    }

    public static void Configure()
    {
        System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";
        System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;
    }
    public static bool TryLoad()
    {
        string settings = "";
        try
        {
            if (!File.Exists(settingsFileName))
            {
                return false;
            }

            settings = File.ReadAllText(settingsFileName);
            Settings.Instance = JsonSerializer.Deserialize(settings, SettingsJsonContext.Default.Settings)!;
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load settings: {e.Message} {e.StackTrace}");
            Console.WriteLine($"Content was: {settings}");
            return false;
        }
    }
    public static void CreateDefault()
    {
        Settings.Instance = new Settings
        {
            ModsDirectory = defaultModsDirectory,
            TotalConversionSandboxPath = GetTotalConversionSandboxDirectory(),
            Ck3Directory = GetGameDirectory(),
        };

        Save();
    }

    public static void Save()
    {
        File.WriteAllText(settingsFileName, JsonSerializer.Serialize(Settings.Instance, SettingsJsonContext.Default.Settings));
    }
}

