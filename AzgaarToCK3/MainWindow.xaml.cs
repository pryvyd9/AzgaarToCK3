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
public record Burg(/*townId*/int i, /*cellId*/int cell, /*townName*/string name, /*provinceId*/int feature, float x, float y);
public class Pack
{
    [JsonConverter(typeof(PackProvinceJsonConverter))]
    public PackProvince[] provinces { get; set; }
    public Burg[] burgs { get; set; }
}


public record JsonMap(Pack pack);


public record Geometry(string type, float[][][] coordinates);
public record Properties(int id, string type, int province, int state, int height, int[] neighbors);
public record Feature(Geometry geometry, Properties properties);
public record GeoMap(Feature[] features);


//public record Province(List<float[][]> cells, MagickColor color);
public record Cell(int id, int height, float[][] cells, int[] neighbors)
{
    public override string ToString()
    {
        return $"id:{id},neighbors:[{string.Join(",", neighbors)}]";
    }
}
//public record Town(int id, int cellId, string name, float x, float y);
public class Province 
{
    public List<Cell> Cells { get; set; } = new();
    // Town
    public Burg Burg { get; set; }
    public MagickColor Color { get; set; }
    public string Name { get; set; }
    public int Id { get; set; }
}


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
        await MapManager.WriteBuildingLocators(map);
        await MapManager.WriteSiegeLocators(map);
        await MapManager.WriteCombatLocators(map);
        await MapManager.WritePlayerStackLocators(map);

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
