using ImageMagick;
using System;
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


public static class Helper
{
    public static string? GetBiomeName(int biomeId, int heightDifference)
    {
        // plains/farmlands/hills/mountains/desert/desert_mountains/oasis/jungle/forest/taiga/wetlands/steppe/floodplains/drylands
        /*
         * 	"Marine",
			"Hot desert",
			"Cold desert",
			"Savanna",
			"Grassland",
			"Tropical seasonal forest",
			"Temperate deciduous forest",
			"Tropical rainforest",
			"Temperate rainforest",
			"Taiga",
			"Tundra",
			"Glacier",
			"Wetland"
         * */
        if (heightDifference > 500)
        {
            return biomeId switch
            {
                0 => null,
                1 or 3 => "desert_mountains",
                _ => "mountains",
            };
        }
        else if (heightDifference > 100 && biomeId is 4 or 5 or 6 or 7 or 8 or 10 or 11)
        {
            return "hills";
        }
        return biomeId switch
        {
            0 => null, // Marine > ocean
            1 => "desert",// Hot desert > desert
            2 => "taiga",// Cold desert > taiga
            3 => "steppe",// Savanna > steppe
            4 => "plains",// Grassland > plains
            5 => "farmlands",// Tropical seasonal forest > farmlands
            6 => "forest",// Temperate deciduous forest > forest
            7 => "jungle",// Tropical rainforest > jungle
            8 => "forest",// "Temperate rainforest" > forest
            9 => "taiga",// Taiga > taiga
            10 => "taiga",// Tundra > taiga
            11 => "floodplains",// Glacier > floodplains
            12 => "wetlands",// Wetland > wetlands
            _ => throw new ArgumentException("Unrecognized biomeId")
        }; ;
    }

    //public static double Percentile(double[] sequence, double excelPercentile)
    //{
    //    Array.Sort(sequence);
    //    int N = sequence.Length;
    //    double n = (N - 1) * excelPercentile + 1;
    //    // Another method: double n = (N + 1) * excelPercentile;
    //    if (n == 1d) return sequence[0];
    //    else if (n == N) return sequence[N - 1];
    //    else
    //    {
    //        int k = (int)n;
    //        double d = n - k;
    //        return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
    //    }
    //}
    public static double Percentile(int[] sequence, double excelPercentile)
    {
        Array.Sort(sequence);
        int N = sequence.Length;
        double n = (N - 1) * excelPercentile + 1;
        // Another method: double n = (N + 1) * excelPercentile;
        if (n == 1d) return sequence[0];
        else if (n == N) return sequence[N - 1];
        else
        {
            int k = (int)n;
            double d = n - k;
            return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
        }
    }
}

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
        //await MapManager.DrawRivers(map);
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
        await MapManager.WriteTerrain(map);

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
