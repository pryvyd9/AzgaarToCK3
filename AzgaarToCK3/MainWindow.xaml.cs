using ImageMagick;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static void Configure()
    {
        System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";

        System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;
    }
    private static async Task<Map> LoadMap()
    {
        var geoMap = await MapManager.LoadGeojson();
        var jsonMap = await MapManager.LoadJson();
        var map = await MapManager.ConvertMap(geoMap, jsonMap);
        return map;
    }

    public static async Task Test()
    {
        var map = await LoadMap();


        //await MapManager.DrawCells(map);

        //await MapManager.DrawProvinces(map);
        //await MapManager.DrawHeightMap(map);
        await MapManager.DrawRivers(map);
        //await MapManager.WriteDefinition(map);

        //await MapManager.WriteBuildingLocators(map);
        //await MapManager.WriteSiegeLocators(map);
        //await MapManager.WriteCombatLocators(map);
        //await MapManager.WritePlayerStackLocators(map);

        //var titles = MapManager.CreateTitles(map);
        //map.Empires = titles;
        //await MapManager.WriteLandedTitles(map.Empires);
        //await MapManager.WriteTitleLocalization(map.Empires);

        //await MapManager.WriteDefault(map);
        //await MapManager.WriteTerrain(map);
        //await MapManager.WriteHillsMask(map);

        Application.Current.Shutdown();
    }
    public MainWindow()
    {
        Configure();

        InitializeComponent();

        _ = Test();

        //Task.Run(() => MapManager.LoadTest());
       
        //var map = MapManager.Load("Mones Full 2024-04-20-12-52.json");

    }
}
