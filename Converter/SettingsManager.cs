using System.Text.Json;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Converter;

public class Settings
{
    public string modsDirectory { get; init; }
    public string ck3Directory { get; init; }
    public string totalConversionSandboxPath { get; init; }
    public string inputJsonPath { get; set; }
    public string inputGeojsonPath { get; set; }
    public string modName { get; set; }
    public bool? shouldOverride { get; set; } = null;
    public bool everyoneIsCount { get; set; } = false;

    [JsonIgnore]
    public static Settings Instance { get; set; }
}

[JsonSerializable(typeof(Settings))]
[JsonSourceGenerationOptions(WriteIndented = true, AllowTrailingCommas = true, PropertyNameCaseInsensitive = false)]
public partial class SettingsJsonContext : JsonSerializerContext { }

public static class SettingsManager
{
    private static readonly string settingsFileName = Helper.GetPath(ExecutablePath, "settings.json");
    private static readonly string defaultModsDirectory = Helper.GetPath(MyDocuments, "Paradox Interactive", "Crusader Kings III", "mod");
    private static readonly string defaultInputJsonPath = Helper.GetPath(ExecutablePath, "input.json");
    private static readonly string defaultInputGeojsonPath = Helper.GetPath(ExecutablePath, "input.geojson");
    private static string MyDocuments => Helper.GetPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    public static string ExecutablePath => Helper.GetPath(Directory.GetParent(Environment.ProcessPath!)!.FullName);
    public static string OutputDirectory => Helper.GetPath(Settings.Instance.modsDirectory, Settings.Instance.modName);

    private static string GetSteamLibraryFoldersPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var steamRegistryKey = Environment.Is64BitOperatingSystem
                ? "HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam"
                : "HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam";

            var steamPath = (string)Registry.GetValue(steamRegistryKey, "InstallPath", null)!;
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
            modsDirectory = defaultModsDirectory,
            inputJsonPath = defaultInputJsonPath,
            inputGeojsonPath = defaultInputGeojsonPath,
            totalConversionSandboxPath = GetTotalConversionSandboxDirectory(),
            ck3Directory = GetGameDirectory(),
        };

        Save();
    }

    public static void Save()
    {
        File.WriteAllText(settingsFileName, JsonSerializer.Serialize(Settings.Instance, SettingsJsonContext.Default.Settings));
    }
}

