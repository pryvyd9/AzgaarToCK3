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


public record PackProvince(int i, int state, int burg, string name);

// For some reason the first element in the array isn't an object but a number.
// Need custom converter to skip the number and parse all other provinces.
public class PackProvinceJsonConverter : JsonConverter<PackProvince[]>
{
    public override PackProvince[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDocument = JsonDocument.ParseValue(ref reader);
        var str = jsonDocument.RootElement.GetRawText();

        // replace 0 with empty province (sea).
        var escapedStr = string.Concat(str.AsSpan(0, 1),"{}", str.AsSpan(2));

        var provinces = JsonSerializer.Deserialize<PackProvince[]>(escapedStr);

        return provinces;
    }

    public override void Write(Utf8JsonWriter writer, PackProvince[] value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}
public record Burg(/*townId*/int i, /*cellId*/int cell, /*townName*/string name, int feature, float x, float y);
public record State(int i, string name, int[] provinces);
public record Culture(int i, string name);
public record Religion(int i, string name);
public class Pack
{
    [JsonConverter(typeof(PackProvinceJsonConverter))]
    public PackProvince[] provinces { get; set; }
    public Burg[] burgs { get; set; }
    public State[] states { get; set; }
    public Culture[] cultures { get; set; }
    public Religion[] religions { get; set; }
}
public record JsonMap(Pack pack);


public record Geometry(string type, float[][][] coordinates);
public record Properties(int id, string type, int province, int state, int height, int[] neighbors, int culture, int religion);
public record Feature(Geometry geometry, Properties properties);
public record GeoMap(Feature[] features);


//public record Province(List<float[][]> cells, MagickColor color);
public record Cell(int id, int height, float[][] cells, int[] neighbors, int culture, int religion)
{
    public override string ToString()
    {
        return $"id:{id},neighbors:[{string.Join(",", neighbors)}]";
    }
}
public class Province 
{
    public List<Cell> Cells { get; set; } = new();
    // Town
    public Burg Burg { get; set; }
    public MagickColor Color { get; set; }
    public string Name { get; set; }
    public int Id { get; set; }
    public int StateId { get; set; }
    public Province[] Neighbors { get; set; } = Array.Empty<Province>();
}

public record Barony(Province province, string name, MagickColor color);
public class County {
    public List<Barony> baronies = new();
    public string Name { get; set; }
    public MagickColor Color { get; set; }
    public string CapitalName { get; set; }
}
public record Duchy(County[] counties, string name, MagickColor color, string capitalName);
public record Kingdom(Duchy[] duchies, bool isAllowed, string name, MagickColor color, string capitalName);
public record Empire(Kingdom[] kingdoms, bool isAllowed, string name, MagickColor color, string capitalName);
public class Map
{
    //public PointD[][] Coordinates { get; set; }
    public GeoMap GeoMap { get; set; }
    public JsonMap JsonMap { get; set; }
    public float XOffset { get; set; }
    public float YOffset { get; set; }
    public float XRatio { get; set; }
    public float YRatio { get; set; }

    public Province[] Provinces { get; set; }
    public Empire[] Empires { get; set; }
}


public class MyJsonTypeInfoResolver : IJsonTypeInfoResolver
{
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}


/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public static async Task Test()
    {
        System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";

        System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

        var geoMap = await MapManager.LoadGeojson();
        var jsonMap = await MapManager.LoadJson();
        var map = await MapManager.ConvertMap(geoMap, jsonMap);

        //await MapManager.DrawCells(map);
        await MapManager.DrawProvinces(map);
        //await MapManager.DrawHeightMap(map);
        //await MapManager.WriteDefinition(map);
        //await MapManager.WriteBuildingLocators(map);
        //await MapManager.WriteSiegeLocators(map);
        //await MapManager.WriteCombatLocators(map);
        //await MapManager.WritePlayerStackLocators(map);
        var titles = MapManager.CreateTitles(map);
        map.Empires = titles;

        await MapManager.WriteLandedTitles(map.Empires);
        await MapManager.WriteTitleLocalization(map.Empires);

        Application.Current.Shutdown();
    }
    public MainWindow()
    {
        InitializeComponent();

        _ = Test();

        //Task.Run(() => MapManager.LoadTest());
       
        //var map = MapManager.Load("Mones Full 2024-04-20-12-52.json");

    }
}
