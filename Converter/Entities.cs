using ImageMagick;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Xml;
using System.Text.Json.Serialization.Metadata;

namespace Converter;

[JsonSerializable(typeof(Input.GridFeature[]))]
public partial class GridFeatureArrayJsonContext : JsonSerializerContext { }

[JsonSerializable(typeof(Input.GridFeature))]
[JsonSerializable(typeof(Input.GridGeneral))]
public partial class GridGeneralJsonContext : JsonSerializerContext { }


public static class Input
{
    // Needed for objects for which there are 0 instead of empty object in input file
    public abstract class AbstractJsonConverter<T> : JsonConverter<T>
    {
        protected abstract JsonTypeInfo<T> JsonTypeInfo { get; }

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var jsonDocument = JsonDocument.ParseValue(ref reader);
            var str = jsonDocument.RootElement.GetRawText();

            // replace 0 with empty object.
            var escapedStr = string.Concat(str.AsSpan(0, 1), "{}", str.AsSpan(2));

            var objects = JsonSerializer.Deserialize(escapedStr, JsonTypeInfo);
            return objects;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }


    public class MapInputReader
    {
        private readonly Queue<string> parameters;

        public MapInputReader(string filename)
        {
            var file = File.ReadAllText(filename);
            parameters = new Queue<string>(file.Split("\r\n"));
        }

        public string? Read() => parameters.TryDequeue(out var val) ? val : null;
    }

    private static T Instantiate<T>(string[] args, Func<string[], string[]>? selector = null)
    {
        var constructors = typeof(T).GetConstructors();
        if (constructors.Length > 1)
        {
            throw new Exception("Type must have only 1 constructor");
        }

        var constructor = constructors[0];
        var parameters = constructor.GetParameters();

        var selectedArgs = selector?.Invoke(args) ?? args;
        if (selectedArgs.Length != parameters.Length)
        {
            throw new Exception("Parameter count must equal constructor argument count");
        }

        var paramArray = parameters.Zip(selectedArgs).Select(n => Convert.ChangeType(n.Second, n.First.ParameterType)).ToArray();
        return (T)Activator.CreateInstance(typeof(T), paramArray)!;
    }
    public static MapInputReader CreateReader(string filename) => new MapInputReader(filename);

    public interface IMapDeserializable<T>
    {
        static abstract T Deserialize(MapInputReader reader);

        //public static virtual T DefaultDeserialize(MapInputReader reader)
        //{
        //    var constructors = typeof(T).GetConstructors();
        //    if (constructors.Length > 1)
        //    {
        //        throw new Exception("Type must have only 1 constructor");
        //    }

        //    var constructor = constructors[0];
        //    var parameters = constructor.GetParameters();

        //    var args = parameters.Select(p =>
        //    {
        //        if (p.ParameterType.GetInterfaces().FirstOrDefault(n => n.IsGenericType && n.GetGenericTypeDefinition() == typeof(IMapDeserializable<>)) is { } @interface)
        //        {
        //            var deserializeMethod = @interface.GetMethod(nameof(Deserialize), BindingFlags.Static | BindingFlags.Public, [typeof(MapInputReader)]);
        //            try
        //            {
        //                return deserializeMethod!.Invoke(null, [reader]);
        //            }
        //            catch (Exception ex)
        //            {
        //                Debugger.Break();
        //                throw new Exception($"Could not instantiate {p.ParameterType.FullName}. Override Deserialize method.");
        //            }

        //            //return deserializeMethod!.GetGenericMethodDefinition().MakeGenericMethod(typeof(T)).Invoke(null, [reader])!;
        //        }
        //        if (p.ParameterType.GetInterface(nameof(IConvertible)) is not null)
        //        {
        //            return Convert.ChangeType(reader.Read()!, p.ParameterType)!;
        //        }

        //        throw new Exception($"Could not instantiate {typeof(T).FullName}. Unsupported argument type: {p.ParameterType.FullName}");
        //    }).ToArray();

        //    return Instantiate<T>(args!);
        //}
    }

    public record Params(string version, string license, string dateString, string seed, int graphWidth, int graphHeight, long mapId) : IMapDeserializable<Params>
    {
        public static Params Deserialize(MapInputReader reader) => Instantiate<Params>(reader.Read()!.Split('|'));
    };
    public record Settings(
        string distanceUnitInput,
        string distanceScaleInput,
        string areaUnit,
        string heightUnit,
        string heightExponentUnit,
        string temperatureScale,
        string populationRate,
        string urbanization,
        string mapSizeOutput,
        string latitudeOutput,
        string precOutput,
        string options, // json
        string mapName,
        string hideLabels,
        string stylePreset,
        string rescaleLabels,
        string urbanDensity) : IMapDeserializable<Settings>
    {
        public static Settings Deserialize(MapInputReader reader)
        {
            var parameters = reader.Read()!;
            return Instantiate<Settings>(parameters.Split('|'), args => [.. args[..6], .. args[12..16], .. args[18..]]);
        }
    }
    public record Biomes(string color, string habitability, string name) : IMapDeserializable<Biomes>
    {
        public static Biomes Deserialize(MapInputReader reader) => Instantiate<Biomes>(reader.Read()!.Split('|'));
    }


    public record GridFeature(int i, bool land, bool border, string type);
    public class GridFeatureJsonConverter : AbstractJsonConverter<GridFeature[]>
    {
        protected override JsonTypeInfo<GridFeature[]> JsonTypeInfo => GridFeatureArrayJsonContext.Default.GridFeatureArray;
    }


    public record GridGeneral(
        float spacing,
        int cellsX,
        int cellsY,
        int[][] boundary,
        float[][] points,
        [property: JsonConverter(typeof(GridFeatureJsonConverter))] GridFeature[] features,
        int cellsDesired);
    public record GridCell(int h, int prec, int f, int t, int temp);
    public record Grid(GridGeneral general, GridCell[] cells) : IMapDeserializable<Grid>
    {
        public static Grid Deserialize(MapInputReader reader)
        {
            var general = JsonSerializer.Deserialize<GridGeneral>(reader.Read()!)!;
            var h = ReadArray();
            var prec = ReadArray();
            var f = ReadArray();
            var t = ReadArray();
            var temp = ReadArray();
            var cells = Enumerable.Range(0, h.Length).Select(i => Instantiate<GridCell>([h[i], prec[i], f[i], t[i], temp[i]])).ToArray();

            return new Grid(general, cells);

            string[] ReadArray() => reader.Read()!.Split(',');
        }
    }
    public record PackCell(int biome, int burg, string conf, int culture, int fl,
        float pop, int r, int road, int s, int state, int religion, int province, int crossroad);
    public record Pack(string packFeatures, // json
        string cultures, // json
        string states, // json
        string burgs, // json
        PackCell[] cells,
        string religions,
        string provinces) : IMapDeserializable<Pack>
    {
        public static Pack Deserialize(MapInputReader reader)
        {
            var packFeatures = reader.Read()!;
            var cultures = reader.Read()!;
            var states = reader.Read()!;
            var burgs = reader.Read()!;

            var biome = ReadArray();
            var burg = ReadArray();
            var conf = ReadArray();
            var culture = ReadArray();
            var fl = ReadArray();
            var pop = ReadArray();
            var r = ReadArray();
            var road = ReadArray();
            var s = ReadArray();
            var state = ReadArray();
            var religion = ReadArray();
            var province = ReadArray();
            var crossroad = ReadArray();
            var cells = Enumerable.Range(0, biome.Length).Select(i => Instantiate<PackCell>(
                [biome[i], burg[i], conf[i], culture[i], fl[i], pop[i], r[i], road[i], s[i], state[i], religion[i], province[i], crossroad[i]]
                )).ToArray();

            string religions = reader.Read()!;
            string provinces = reader.Read()!;

            return new Pack(packFeatures, cultures, states, burgs, cells, religions, provinces);

            string[] ReadArray() => reader.Read()!.Split(',');
        }
    }
    //public record Coords(float latT, float latN, float latS, float lonT, float LonW, float lonE);
    public record Map(
        Params @params,
        Settings settings,
        string coords, // json
        Biomes biomes,
        string notesDate, // json
        XmlDocument svg,
        Grid grid,
        //string packFeatures, // json
        //string cultures, // json
        //string states, // json
        //string burgs, // json
        Pack pack,
        //string religions,
        //string provinces,
        string namesData,
        string rivers,
        string rulersString,
        string fonts,
        string markers
        ) : IMapDeserializable<Map>
    {

        public static Map Deserialize(MapInputReader reader)
        {
            var @params = Params.Deserialize(reader);
            var settings = Settings.Deserialize(reader);
            var coords = reader.Read()!;
            var biomes = Biomes.Deserialize(reader);
            var notesDate = reader.Read()!;

            var svgStr = reader.Read()!;
            var svg = new XmlDocument();
            svg.LoadXml(svgStr);

            var grid = Grid.Deserialize(reader);
            //var packFeatures = reader.Read()!;
            //var cultures = reader.Read()!;
            //var states = reader.Read()!;
            //var burgs = reader.Read()!;
            var pack = Pack.Deserialize(reader);
            //var religions = reader.Read()!;
            //var provinces = reader.Read()!;
            var namesData = reader.Read()!;
            var rivers = reader.Read()!;
            var rulersString = reader.Read()!;
            var fonts = reader.Read()!;
            var markers = reader.Read()!;

            return new Map(
                @params,
                settings,
                coords,
                biomes,
                notesDate,
                svg,
                grid,
                pack,
                namesData,
                rivers,
                rulersString,
                fonts,
                markers);
        }
    }
}

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
        public required Input.Map InputMap { get; init; }
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

