using ImageMagick;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Xml;

namespace Converter;



//public record CK3Religion()

public record GeometryRivers(string type, float[][] coordinates);
public record FeatureRivers(GeometryRivers geometry);
public record GeoMapRivers(FeatureRivers[] features);


public record PackProvince(int i, int state, int burg, string name);

[JsonSerializable(typeof(PackProvince[]))]
public partial class PackProinvceArrayJsonContext : JsonSerializerContext { }

// For some reason the first element in the array isn't an object but a number.
// Need custom converter to skip the number and parse all other provinces.
public class PackProvinceJsonConverter : JsonConverter<PackProvince[]>
{
    public override PackProvince[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDocument = JsonDocument.ParseValue(ref reader);
        var str = jsonDocument.RootElement.GetRawText();

        // replace 0 with empty province.
        var escapedStr = string.Concat(str.AsSpan(0, 1), "{}", str.AsSpan(2));

        var provinces = JsonSerializer.Deserialize(escapedStr, PackProinvceArrayJsonContext.Default.PackProvinceArray);

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
public record PackCell(int i, int area, int biome);
public record MapCoordinates(float latT, float latN, float latS, float lonT, float lonW, float lonE);
public class Pack
{
    [JsonConverter(typeof(PackProvinceJsonConverter))]
    public PackProvince[] provinces { get; set; }
    public Burg[] burgs { get; set; }
    public State[] states { get; set; }
    public Culture[] cultures { get; set; }
    public Religion[] religions { get; set; }
    public PackCell[] cells { get; set; }
}
public record Info(int width, int height);
public record NameBase(string name, string b);
public record JsonMap(Pack pack, MapCoordinates mapCoordinates, Info info, NameBase[] nameBases);

public record Geometry(string type, float[][][] coordinates);
public record Properties(int id, string type, int province, int state, int height, int[] neighbors, int culture, int religion);
public record Feature(Geometry geometry, Properties properties);
public record GeoMap(Feature[] features);


public record Cell(int id, int height, float[][] cells, int[] neighbors, int culture, int religion, int area, int biome)
{
    public override string ToString()
    {
        return $"id:{id},neighbors:[{string.Join(",", neighbors)}]";
    }
}
public class Province
{
    public List<Cell> Cells { get; set; } = [];
    // Town
    public Burg Burg { get; set; }
    public MagickColor Color { get; set; }
    public string Name { get; set; }
    public int Id { get; set; }
    public int StateId { get; set; }
    public Province[] Neighbors { get; set; } = [];
    public bool IsWater { get; set; }
}

// dynasty can repeat.
public record Character(string id, NameBaseName name, string culture, string religion, int age, int stewardshipSkill, NameBaseName dynastyName);

public interface ITitle
{
    string Id { get; }
}
public interface ICultureReligionHolder
{
    string Culture { get; }
    string Religion { get; }
}

public record Barony(int id, Province province, string name, MagickColor color) : ICultureReligionHolder, ITitle
{
    public string Culture { get; set; }
    public string Religion { get; set; }

    public string Id => $"b_{id}";
}
public class County : ITitle, ICultureReligionHolder
{
    public int id { get; init; }
    public List<Barony> baronies = new();
    public string Name { get; init; }
    public MagickColor Color { get; init; }
    public string CapitalName { get; init; }
    public ITitle liege;

    public Character holder;
    public string Id => $"c_{id}";
    public string Culture { get; set; }
    public string Religion { get; set; }
}
public record Duchy(int id, County[] counties, string name, MagickColor color, string capitalName) : ITitle, ICultureReligionHolder
{
    public Character holder;
    public ITitle liege;

    public string Id => $"d_{id}";
    public string Culture { get; set; }
    public string Religion { get; set; }
}
public record Kingdom(int id, Duchy[] duchies, bool isAllowed, string name, MagickColor color, string capitalName) : ITitle, ICultureReligionHolder
{
    public Character holder;
    public ITitle liege;

    public string Id => $"k_{id}";
    public string Culture { get; set; }
    public string Religion { get; set; }
};
public record Empire(int id, Kingdom[] kingdoms, bool isAllowed, string name, MagickColor color, string capitalName) : ITitle, ICultureReligionHolder
{
    public Character holder;

    public string Id => $"e_{id}";
    public string Culture { get; set; }
    public string Religion { get; set; }
};

public record NameBaseName(string id, string name);
public record NameBasePrepared(string name, NameBaseName[] names);
public class Map
{
    public class MapInput
    {
        public required GeoMap GeoMap { get; init; }
        public required GeoMapRivers Rivers { get; init; }
        public required JsonMap JsonMap { get; init; }
        public required XmlDocument XmlMap { get; init; }
    }
    public required MapInput Input { get; init; }

    public class MapOutput
    {
        public Province[]? Provinces { get; set; }
        public Empire[]? Empires { get; set; }

        public List<Character>? Characters { get; set; }
        public NameBasePrepared? NameBase { get; set; }
        public Dictionary<int, int>? IdToIndex { get; set; }
    }
    public required MapOutput Output { get; init; }


    //public const int MapWidth = 8192;
    //public const int MapHeight = 4096;

    public float XOffset => Input.JsonMap.mapCoordinates.lonW;
    public float YOffset => Input.JsonMap.mapCoordinates.latS;
    public float XRatio => Settings.MapWidth / Input.JsonMap.mapCoordinates.lonT;
    public float YRatio => Settings.MapHeight / Input.JsonMap.mapCoordinates.latT;

    public double PixelXRatio => (double)Settings.MapWidth / Input.JsonMap.info.width;
    public double PixelYRatio => (double)Settings.MapHeight / Input.JsonMap.info.height;

    public Settings Settings { get; set; }
}

