using ImageMagick;
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

public record Settings(string modName, string modsFolderPath, string ck3Directory);

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const string settingsFileName = "settings.json";
    private static Settings _settings;

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
    private void Configure()
    {
        System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";
        System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

        if (!File.Exists(settingsFileName))
        {
            var defaultModName = "MyMap";
            _settings = new Settings(
              defaultModName,
              $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/Paradox Interactive/Crusader Kings III/mod",
              GetGameDirectory()
              );
            File.WriteAllText(settingsFileName, JsonSerializer.Serialize(_settings));
        }
        else
        {
            var settings = File.ReadAllText(settingsFileName);
            _settings = JsonSerializer.Deserialize<Settings>(settings);
        }
    }
    private static async Task<Map> LoadMap()
    {
        var geoMap = await MapManager.LoadGeojson();
        //var geoMapRivers = await MapManager.LoadGeojsonRivers();
        var geoMapRivers = new GeoMapRivers(new FeatureRivers[0]);
        var jsonMap = await MapManager.LoadJson();
        var map = await MapManager.ConvertMap(geoMap, geoMapRivers, jsonMap);
        map.Settings = _settings;
        return map;
    }

    //public static async Task Run()
    //{
    //    try
    //    {
    //        //await ConfigReader.GetCK3Religions(_settings);

    //        var map = await LoadMap();


    //        //await MapManager.DrawCells(map);

    //        //await MapManager.DrawProvinces(map);
    //        //await MapManager.DrawHeightMap(map);
    //        //await MapManager.DrawRivers(map);
    //        //await MapManager.WriteDefinition(map);

    //        //await MapManager.WriteLocators(map);

    //        var titles = MapManager.CreateTitles(map);
    //        map.Empires = titles;
    //        await MapManager.WriteLandedTitles(map);
    //        await MapManager.WriteTitleLocalization(map);

    //        var faiths = await MapManager.WriteHistoryProvinces(map);
    //        await MapManager.WriteHolySites(map, faiths);

    //        //await MapManager.WriteDefault(map);
    //        //await MapManager.WriteTerrain(map);
    //        //await MapManager.WriteMasks(map);

    //        //await MapManager.WriteGraphics();
    //    }
    //    catch (Exception ex)
    //    {
    //        Debugger.Break();
    //    }
    //}

    public static async Task Test()
    {
        try
        {
            await ConfigReader.GetCK3Religions(_settings);

            var map = await LoadMap();


            //await MapManager.DrawCells(map);

            //await MapManager.DrawProvinces(map);
            //await MapManager.DrawHeightMap(map);
            //await MapManager.DrawRivers(map);
            //await MapManager.WriteDefinition(map);

            await MapManager.WriteLocators(map);

            var titles = MapManager.CreateTitles(map);
            map.Empires = titles;
            await MapManager.WriteLandedTitles(map);
            await MapManager.WriteTitleLocalization(map);

            var faiths = await MapManager.WriteHistoryProvinces(map);
            await MapManager.CopyOriginalReligions(map);
            await MapManager.WriteHolySites(map, faiths);

            //await MapManager.WriteDefault(map);
            //await MapManager.WriteTerrain(map);
            //await MapManager.WriteMasks(map);

            await MapManager.WriteGraphics();

            Application.Current.Shutdown();
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
#if DEBUG
        _ = Test();
#endif
    }

    private void StartButtonClick(object sender, RoutedEventArgs e)
    {
        _ = Test();
    }
}
