using System.Text.Json;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace Converter;

public class Settings
{
    public string modName { get; set; }
    public string modsDirectory { get; set; }
    public string ck3Directory { get; set; }
    public string totalConversionSandboxPath { get; set; }
    public string inputJsonPath { get; set; }
    public string inputGeojsonPath { get; set; }
    public bool? shouldOverride { get; set; } = null;
}

[JsonSerializable(typeof(Settings))]
public partial class SettingsJsonContext : JsonSerializerContext { }

public static class SettingsManager
{
    private static readonly string settingsFileName = Path.Combine(ExecutablePath, "settings.json");
    private static readonly string defaultModsDirectory = Path.Combine(MyDocuments, "Paradox Interactive", "Crusader Kings III", "mod");
    private static readonly string defaultInputJsonPath = Path.Combine(ExecutablePath, "input.json");
    private static readonly string defaultInputGeojsonPath = Path.Combine(ExecutablePath, "input.geojson");
    private static string MyDocuments => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public static string ExecutablePath => Directory.GetParent(Environment.ProcessPath!)!.FullName;

    public static Settings Instance { get; private set; }

    private static string GetSteamLibraryFoldersPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var steamRegistryKey = Environment.Is64BitOperatingSystem
                ? "HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam"
                : "HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam";

            var steamPath = (string)Registry.GetValue(steamRegistryKey, "InstallPath", null)!;
            return Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        }
        else if (OperatingSystem.IsMacOS())
        {
            var steamPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Steam");
            return Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
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
        
        var ck3Directories = paths.Select(n => Path.Combine(n, "steamapps", "common", "Crusader Kings III", "game")).Where(Directory.Exists).ToArray();
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

        var ck3Directories = paths.Select(n => Path.Combine(n, "steamapps", "workshop", "content", "1158310", "2524797018")).Where(Directory.Exists).ToArray();
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
            Instance = JsonSerializer.Deserialize(settings, SettingsJsonContext.Default.Settings)!;
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
        Instance = new Settings
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
        File.WriteAllText(settingsFileName, JsonSerializer.Serialize(Instance, SettingsJsonContext.Default.Settings));
    }
}

