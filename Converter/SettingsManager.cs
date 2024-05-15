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
    private static string settingsFileName = $"{Environment.CurrentDirectory}/settings.json";
    public static Settings Settings { get; set; }

    private static string GetGameDirectoryWindows()
    {
        var steamRegistryKey = Environment.Is64BitOperatingSystem
            ? "HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam"
            : "HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam";

        var steamPath = Registry.GetValue(steamRegistryKey, "InstallPath", null);
        var libraries = File.ReadAllText($"{steamPath}/steamapps/libraryfolders.vdf");
        var pathRegex = new Regex("\"path\"\\s*\"(.+)\"");
        var paths = pathRegex.Matches(libraries).Select(n => n.Groups[1].Value);

        var ck3Directories = paths.Select(n => $"{n}/steamapps/common/Crusader Kings III/game").Where(Directory.Exists).ToArray();
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
    private static string GetGameDirectoryMac()
    {
        var steamPath = "~/Library/Application Support/Steam";
        var libraries = File.ReadAllText($"{steamPath}/steamapps/libraryfolders.vdf");
        var pathRegex = new Regex("\"path\"\\s*\"(.+)\"");
        var paths = pathRegex.Matches(libraries).Select(n => n.Groups[1].Value);

        var ck3Directories = paths.Select(n => $"{n}/steamapps/common/Crusader Kings III/game").Where(Directory.Exists).ToArray();
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

    private static string GetTotalConversionSandboxDirectoryWindows()
    {
        var steamRegistryKey = Environment.Is64BitOperatingSystem
            ? "HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Valve\\Steam"
            : "HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam";

        var steamPath = Registry.GetValue(steamRegistryKey, "InstallPath", null);
        var libraries = File.ReadAllText($"{steamPath}/steamapps/libraryfolders.vdf");
        var pathRegex = new Regex("\"path\"\\s*\"(.+)\"");
        var paths = pathRegex.Matches(libraries).Select(n => n.Groups[1].Value);

        var ck3Directories = paths.Select(n => $"{n}/steamapps/workshop/content/1158310/2524797018").Where(Directory.Exists).ToArray();
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
    private static string GetTotalConversionSandboxDirectoryMac()
    {
        var steamPath = "~/Library/Application Support/Steam";
        var libraries = File.ReadAllText($"{steamPath}/steamapps/libraryfolders.vdf");
        var pathRegex = new Regex("\"path\"\\s*\"(.+)\"");
        var paths = pathRegex.Matches(libraries).Select(n => n.Groups[1].Value);

        var ck3Directories = paths.Select(n => $"{n}/steamapps/workshop/content/1158310/2524797018").Where(Directory.Exists).ToArray();
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
        try
        {
            if (!File.Exists(settingsFileName))
            {
                return false;
            }

            var settings = File.ReadAllText(settingsFileName);
            Settings = JsonSerializer.Deserialize(settings, SettingsJsonContext.Default.Settings)!;
            return true;
        } 
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load settings: {e.Message} {e.StackTrace}");
            return false;
        }
    }
    public static void CreateDefault()
    {
        if (OperatingSystem.IsWindows())
        {
            Settings = new Settings
            {
                modsDirectory = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/Paradox Interactive/Crusader Kings III/mod"
                    .Replace(@"\\", "/").Replace(@"\", "/"),
                ck3Directory = GetGameDirectoryWindows()
                    .Replace(@"\\", "/").Replace(@"\", "/"),
                totalConversionSandboxPath = GetTotalConversionSandboxDirectoryWindows()
                    .Replace(@"\\", "/").Replace(@"\", "/"),
                inputJsonPath = $"{Environment.CurrentDirectory}/input.json"
                    .Replace(@"\\", "/").Replace(@"\", "/"),
                inputGeojsonPath = $"{Environment.CurrentDirectory}/input.geojson"
                    .Replace(@"\\", "/").Replace(@"\", "/"),
            };
        }
        else if (OperatingSystem.IsMacOS())
        {
            Settings = new Settings
            {
                modsDirectory = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/Paradox Interactive/Crusader Kings III/mod"
                    .Replace(@"\\", "/").Replace(@"\", "/"),
                ck3Directory = GetGameDirectoryMac()
                    .Replace(@"\\", "/").Replace(@"\", "/"),
                totalConversionSandboxPath = GetTotalConversionSandboxDirectoryMac()
                    .Replace(@"\\", "/").Replace(@"\", "/"),
                inputJsonPath = $"{Environment.CurrentDirectory}/input.json"
                    .Replace(@"\\", "/").Replace(@"\", "/"),
                inputGeojsonPath = $"{Environment.CurrentDirectory}/input.geojson"
                    .Replace(@"\\", "/").Replace(@"\", "/"),
            };
        }

        Save();
    }

    public static void Save()
    {
        File.WriteAllText(settingsFileName, JsonSerializer.Serialize(Settings, SettingsJsonContext.Default.Settings));
    }
}

