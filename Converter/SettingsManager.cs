using System.Text.Json;
using System.Text.Json.Serialization;

namespace Converter;

public class Settings
{
    public required string ModsDirectory { get; init; }
    public required string Ck3Directory { get; init; }
    public required string TotalConversionSandboxPath { get; init; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public string InputXmlPath { get; set; }
    public string ModName { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public bool? ShouldOverride { get; set; } = null;
    public bool OnlyCounts { get; set; } = false;
    public int MapWidth { get; set; } = 8192;
    public int MapHeight { get; set; } = 4096;
    public int MaxThreads { get; set; } = Environment.ProcessorCount;

    [JsonIgnore]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public static Settings Instance { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [JsonIgnore]
    public static string OutputDirectory => Helper.GetPath(Instance.ModsDirectory, Instance.ModName);


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
    public static readonly string SettingsFileName = Helper.GetPath(ExecutablePath, "settings.json");
    public static string ExecutablePath => Helper.GetPath(Directory.GetParent(Environment.ProcessPath!)!.FullName);

    public static void Configure()
    {
        CK3FileSystem.ConfigureNumberDecimalSeparator();
    }
    public static bool TryLoad()
    {
        try
        {
            if (!File.Exists(SettingsFileName))
            {
                return false;
            }

            var settings = File.ReadAllText(SettingsFileName);
            return TryLoadFromString(settings);
        } 
        catch (Exception e)
        {
            MyConsole.Error($"Failed to load settings: {e.Message} {e.StackTrace}");
            return false;
        }
    }
    public static bool TryLoadFromString(string settingsJson)
    {
        try { 
            Settings.Instance = JsonSerializer.Deserialize(settingsJson, SettingsJsonContext.Default.Settings)!;
            return true;
        }
        catch (Exception e)
        {
            MyConsole.Error($"Failed to load settings from json: {e.Message} {e.StackTrace}");
            MyConsole.Error($"Content was: {settingsJson}");
            return false;
        }
    }
    public static string ToJson()
    {
        return JsonSerializer.Serialize(Settings.Instance, SettingsJsonContext.Default.Settings);
    }
    public static void CreateDefault()
    {
        Settings.Instance = new Settings
        {
            ModsDirectory =  CK3FileSystem.defaultModsDirectory,
            TotalConversionSandboxPath = CK3FileSystem.GetTotalConversionSandboxDirectory(),
            Ck3Directory = CK3FileSystem.GetGameDirectory(),
        };

        Save();
    }

    public static void Save()
    {
        File.WriteAllText(SettingsFileName, JsonSerializer.Serialize(Settings.Instance, SettingsJsonContext.Default.Settings));
    }
}
