using ImageMagick;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AzgaarToCK3;

//public record Settings(string modName, string modsDirectory, string ck3Directory, string totalConversionSandboxPath);
public class Settings 
{
    public string modName { get; set; }
    public string modsDirectory { get; set; }
    public string ck3Directory { get; set; }
    public string totalConversionSandboxPath { get; set; }
    public string inputJsonPath { get; set; }
    public string inputGeojsonPath { get; set; }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static string settingsFileName = $"{Environment.CurrentDirectory}/settings.json";
    public static Settings Settings { get; set; }

    private static string GetGameDirectory()
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
    private static string GetTotalConversionSandboxDirectory()
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
    private static void Configure()
    {
        System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";
        System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

        if (!File.Exists(settingsFileName))
        {
            var defaultModName = "MyMap";
            Settings = new Settings
            {
                modName = defaultModName,
                modsDirectory = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/Paradox Interactive/Crusader Kings III/mod",
                ck3Directory = GetGameDirectory(),
                totalConversionSandboxPath = GetTotalConversionSandboxDirectory(),
                inputJsonPath = $"{Environment.CurrentDirectory}/input.json",
                inputGeojsonPath = $"{Environment.CurrentDirectory}/input.geojson",
            };
            File.WriteAllText(settingsFileName, JsonSerializer.Serialize(Settings));
        }
        else
        {
            var settings = File.ReadAllText(settingsFileName);
            Settings = JsonSerializer.Deserialize<Settings>(settings);
        }
    }
    private static async Task<Map> LoadMap()
    {
        var geoMap = await MapManager.LoadGeojson();
        var geoMapRivers = new GeoMapRivers(Array.Empty<FeatureRivers>());
        var jsonMap = await MapManager.LoadJson();
        var map = await MapManager.ConvertMap(geoMap, geoMapRivers, jsonMap);
        map.Settings = Settings;
        return map;
    }

    private static async Task CreateMod()
    {
        var outsideDescriptor = $@"version=""1.0""
tags={{
	""Total Conversion""
}}
name=""{Settings.modName}""
supported_version=""1.12.4""
path=""mod/{Settings.modName}""";

        await File.WriteAllTextAsync($"{Settings.modsDirectory}/{Settings.modName}.mod", outsideDescriptor);

        FileSystem.CopyDirectory(Settings.totalConversionSandboxPath, $"{Settings.modsDirectory}/{Settings.modName}", true);

        var insideDescriptor = $@"version=""1.0""
tags={{
	""Total Conversion""
}}
name=""{Settings.modName}""
supported_version=""1.12.4""";
        await File.WriteAllTextAsync($"{Settings.modsDirectory}/{Settings.modName}/descriptor.mod", insideDescriptor);
    }

    public static async Task Run()
    {
        try
        {
#if RELEASE
            if (!Directory.Exists($"{Settings.modsDirectory}/{Settings.modName}"))
            {
                await CreateMod();
            }
#endif

            var map = await LoadMap();

            //await MapManager.DrawCells(map);

            await MapManager.DrawProvinces(map);
            await MapManager.DrawHeightMap(map);
            await MapManager.DrawRivers(map);
            await MapManager.WriteDefinition(map);

            await MapManager.WriteLocators(map);

            var titles = MapManager.CreateTitles(map);
            map.Empires = titles;
            await MapManager.WriteLandedTitles(map);
            await MapManager.WriteTitleLocalization(map);

            var faiths = await MapManager.WriteHistoryProvinces(map);
            await MapManager.CopyOriginalReligions(map);
            await MapManager.WriteHolySites(map, faiths);

            await MapManager.WriteDefault(map);
            await MapManager.WriteTerrain(map);
            await MapManager.WriteMasks(map);

            await MapManager.WriteGraphics();

#if DEBUG
            Application.Current.Shutdown();
#endif
        }
        catch (Exception ex)
        {
            Debugger.Break();
        }
      
    }
    public MainWindow()
    {
        Configure();

        InitializeComponent();

        this.DataContext = this;

        gamePathTextBox.Text = Settings.ck3Directory;
        totalConversionModSandboxPath.Text = Settings.totalConversionSandboxPath;
        modsDirectory.Text = Settings.modsDirectory;
        modName.Text = Settings.modName;
        inputJsonPath.Text = Settings.inputJsonPath;
        inputGeojsonPath.Text = Settings.inputGeojsonPath;


#if DEBUG
        _ = Run();
#endif
    }

    private void StartButtonClick(object sender, RoutedEventArgs e)
    {
        _ = Run();
    }

    private void gamePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Settings.ck3Directory = ((TextBox)sender).Text;
    }

    private void totalConversionModSandboxPath_TextChanged(object sender, TextChangedEventArgs e)
    {
        Settings.totalConversionSandboxPath = ((TextBox)sender).Text;
    }

    private void modsDirectory_TextChanged(object sender, TextChangedEventArgs e)
    {
        Settings.modsDirectory = ((TextBox)sender).Text;
    }

    private void modName_TextChanged(object sender, TextChangedEventArgs e)
    {
        Settings.modName = ((TextBox)sender).Text;
    }

    private void inputJsonPath_TextChanged(object sender, TextChangedEventArgs e)
    {
        Settings.inputJsonPath = ((TextBox)sender).Text;
    }

    private void inputGeojsonPath_TextChanged(object sender, TextChangedEventArgs e)
    {
        Settings.inputGeojsonPath = ((TextBox)sender).Text;
    }
}
