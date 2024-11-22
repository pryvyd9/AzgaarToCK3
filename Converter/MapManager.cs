using ImageMagick;
using Microsoft.VisualBasic.FileIO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Svg;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace Converter;

// These are needed for AOT compilation.
[JsonSerializable(typeof(GeoMap))]
public partial class GeoMapJsonContext : JsonSerializerContext {}

[JsonSerializable(typeof(PackProvince))]
[JsonSerializable(typeof(JsonMap))]
public partial class JsonMapJsonContext : JsonSerializerContext {}

public static class MapManager
{
    public static async Task<XmlDocument> LoadXml()
    {
        try
        {
            var unescapedFile = File.ReadAllText(Settings.Instance.InputXmlPath);
            unescapedFile = unescapedFile.Replace("&amp;quot;", "\"");
            unescapedFile = unescapedFile.Replace("xmlns=\"http://www.w3.org/2000/svg\"", "");
            unescapedFile = unescapedFile.Replace("xmlns:dc=\"http://purl.org/dc/elements/1.1/\"", "");
            unescapedFile = unescapedFile.Replace("xmlns:xlink=\"http://www.w3.org/1999/xlink\"", "");
            unescapedFile = unescapedFile.Replace("xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"", "");
            unescapedFile = unescapedFile.Replace("xmlns=\"http://www.w3.org/1999/xhtml\"", "");
            unescapedFile = unescapedFile.Replace("xmlns:svg=\"http://www.w3.org/2000/svg\"", "");

            var file = new XmlDocument();
            file.LoadXml(unescapedFile);

            return file;
        }
        catch (Exception e)
        {
            Debugger.Break();
            throw;
        }
    }

    private static MagickColor GetColor(int i, int maxI)
    {
        // max 24bit color
        const int maxColor = 256 * 256 * 256;
        var color = maxColor / maxI * i;

        byte r = (byte)((color & 0x0000FF) >> 0);
        byte g = (byte)((color & 0x00FF00) >> 8);
        byte b = (byte)((color & 0xFF0000) >> 16);

        var c = new MagickColor(r, g, b);

        return c;
    }

    private static Dictionary<int, Province> GetProvinceCells(GeoMap geomap, JsonMap jsonmap)
    {
        var provinces = new Dictionary<int, Province>();
        foreach (var feature in geomap.features)
        {
            var provinceId = feature.properties.province;

            if (!provinces.ContainsKey(provinceId))
            {
                provinces[provinceId] = new Province()
                {
                    StateId = jsonmap.pack.provinces[provinceId].state
                };
            }

            var cells = feature.geometry.coordinates.Select(n =>
                new Cell(
                    feature.properties.id,
                    feature.properties.height,
                    n,
                    feature.properties.neighbors,
                    feature.properties.culture,
                    feature.properties.religion,
                    jsonmap.pack.cells[feature.properties.id].area,
                    jsonmap.pack.cells[feature.properties.id].biome));
            provinces[provinceId].Cells.AddRange(cells);
        }

        return provinces;
    }
    // Remove 1 cell islands from all provinces.
    private static Province[] TransferHangingCells(Province[] nonWaterProvinces)
    {
        try
        {
            var newProvinces = nonWaterProvinces.ToList();

            // province to where to transfer to. What to transfer.
            var cellsToTransfer = new Dictionary<Province, Cell>();

            // Find cells that don't touch the province but still belong to it.
            // Reassign it to the neighbor province.
            foreach (var province in nonWaterProvinces)
            {
                var cells = province.Cells;
                var cellsToRemove = new List<Cell>();
                foreach (var cell in cells)
                {
                    if (!cells.Any(m => cell.neighbors.Contains(m.id)))
                    {
                        var nonWaterNeighborProvince = nonWaterProvinces.FirstOrDefault(p =>
                        {
                            return p.Cells.Any(c => cell.neighbors.Contains(c.id));
                        });

                        if (nonWaterNeighborProvince is null)
                        {
                            // We don't want 1 cell islands. They are too small to contain locators.
                            // Remove 1 cell islands from provinces completely.
                            cellsToRemove.Add(cell);
                            continue;
                        }

                        cellsToTransfer[nonWaterNeighborProvince] = cell;
                        cellsToRemove.Add(cell);
                    }
                }

                cellsToRemove.ForEach(n => cells.Remove(n));

                // Remove empty provinces
                if (cells.Count == 0)
                {
                    newProvinces.Remove(province);
                }
            }

            // Transfer cells
            foreach (var (p, c) in cellsToTransfer)
            {
                p.Cells.Add(c);
            }

            return newProvinces.ToArray();
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }
    private static List<Province> CreateWaterProvinces(Province waterProvince)
    {
        var logEverySeconds = 30;
        var timer = new System.Timers.Timer(logEverySeconds * 1000);
        timer.AutoReset = true;
        timer.Start();

        try
        {
            var cells = waterProvince.Cells;

            var largestWaterProvince = cells.MaxBy(n => n.area).area;
            var cellsBy128 = cells.Count / 128;
            var areaPerProvince = largestWaterProvince > cellsBy128
                ? largestWaterProvince / 2
                : cellsBy128 / 2;

            var unprocessedCells = waterProvince.Cells.ToDictionary(n => n.id, n => n);
            var provinces = new List<Province>();

            timer.Elapsed += (s, e) =>
            {
                MyConsole.WriteLine($"Water provinces created: {provinces.Count}...");
            };

            do
            {
                var currentCell = unprocessedCells.Values.FirstOrDefault();
                if (currentCell is null)
                {
                    break;
                }

                var currentArea = 0;
                // First accumulate smaller provinces. It will create a more convex shape.
                var accumulatedNeighbors = new List<Cell>();

                provinces.Add(new Province());

                for (int i = 0; currentArea < areaPerProvince; i++)
                {
                    unprocessedCells.Remove(currentCell.id);
                    provinces.Last().Cells.Add(currentCell);
                    currentArea += currentCell.area;

                    foreach (var n in currentCell.neighbors.Where(unprocessedCells.ContainsKey))
                    {
                        // If cell is not found then it's not water cell. So ignore it.
                        if (cells.FirstOrDefault(m => m.id == n) is { } cell)
                        {
                            accumulatedNeighbors.Add(cell);
                        }
                    }

                    if (accumulatedNeighbors.FirstOrDefault() is { } neighbor)
                    {
                        currentCell = neighbor;
                        accumulatedNeighbors.Remove(neighbor);
                    }
                    else
                    {
                        break;
                    }
                }
            } while (unprocessedCells.Count > 0);

            timer.Stop();

            if (provinces.Count > short.MaxValue)
            {
                throw new Exception($"Water province count exceeded max supported value of {short.MaxValue}");
            }

            return provinces;
        }
        catch (Exception ex)
        {
            Debugger.Break();
            timer.Stop();

            throw;
        }
    }
    private static Province[] CreateProvinces(GeoMap geomap, JsonMap jsonmap)
    {
        var provinceCells = GetProvinceCells(geomap, jsonmap);
        var waterProvinces = CreateWaterProvinces(provinceCells[0]);
        var provinces = new Province[provinceCells.Count + waterProvinces.Count];

        // pId == 0 is not an ocean.
        // It's some system thing that needs to exist in order for all indices to start from 1.
        provinces[0] = new Province
        {
            Color = MagickColors.Black,
            Name = "x",
            Id = 0,
        };

        try
        {
            var neighborCellIds = new Dictionary<int, int[]>();
            for (int i = 1; i < provinceCells.Count; i++)
            {
                var color = GetColor(i, provinces.Length);
                var province = provinces[i] = provinceCells[i];

                province.Color = color;
                province.Name = jsonmap.pack.provinces[i].name;
                province.Id = jsonmap.pack.provinces[i].i;
                province.Burg = jsonmap.pack.burgs[jsonmap.pack.provinces[i].burg];

                var cellIds = province.Cells.Select(n => n.id).ToHashSet();
                neighborCellIds[i] = province.Cells.SelectMany(n => n.neighbors.Where(m => !cellIds.Contains(m))).ToArray();
            }

            // Create sea provinces
            for (int i = 0; i < waterProvinces.Count; i++)
            {
                var province = provinces[provinceCells.Count + i] = waterProvinces[i];
                province.Color = GetColor(provinceCells.Count + i, provinces.Length);
                province.Name = "sea";
                province.Id = provinceCells.Count + i;
                province.IsWater = true;
            }

            // Populate neighbors
            for (int i = 0; i < provinceCells.Count; i++)
            {
                var neighbors = new HashSet<Province>();

                if (neighborCellIds.TryGetValue(i, out var cellIds))
                {
                    var processedNeighbors = new HashSet<int>();
                    foreach (var cid in cellIds)
                    {
                        if (processedNeighbors.Contains(cid)) continue;

                        foreach (var p in provinces.Where(n => n.Id != 0 && !n.IsWater && n.StateId == provinces[i].StateId && n.Cells.Any(m => m.id == cid)))
                        {
                            neighbors.Add(p);
                        }

                        processedNeighbors.Add(cid);
                    }
                    provinces[i].Neighbors = neighbors.ToArray();
                }
            }
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }

        var finalProvinces = provinces
            .Take(1)
            .Concat(TransferHangingCells(provinces[1..provinceCells.Count]))
            .Concat(provinces[provinceCells.Count..])
            .ToArray();

        return finalProvinces;
    }

    public static async Task<Map> ConvertMap(XmlDocument xmlMap)
    {
        XmlNamespaceManager xmlnsManager = new(xmlMap.NameTable);
        xmlnsManager.AddNamespace("ns", "http://www.w3.org/1999/xhtml");

        XmlNode? GetNode(string attribute) => xmlMap.SelectSingleNode($"//*[{attribute}]", xmlnsManager);
        var geoMapXml = GetNode("@id='geojson'")!.InnerXml;
        var geoMap = JsonSerializer.Deserialize(geoMapXml, GeoMapJsonContext.Default.GeoMap);

        var jsonMapXml = GetNode("@id='json'")!.InnerXml;
        var jsonMap = JsonSerializer.Deserialize(jsonMapXml, JsonMapJsonContext.Default.JsonMap);

        var geoMapRivers = new GeoMapRivers([]);



        var provinces = CreateProvinces(geoMap!, jsonMap!);

        var rnd = new Random(1);
        var nameBase = jsonMap!.nameBases[rnd.Next(jsonMap.nameBases.Length)];
        var nameBaseNames = nameBase.b.Split(',')
            .Select(n =>
            {
                var id = n.Replace("'", "").Replace(' ', '_').ToLowerInvariant();
                return new NameBaseName(id, n);
            }).ToArray();

        var map = new Map
        {
            Input = new()
            {
                GeoMap = geoMap!,
                Rivers = geoMapRivers,
                JsonMap = jsonMap,
                XmlMap = xmlMap,
            },
            Output = new()
            {
                Provinces = provinces,
                IdToIndex = provinces.Select((n, i) => (n, i)).ToDictionary(n => n.n.Id, n => n.i),
                NameBase = new NameBasePrepared(nameBase.name, nameBaseNames),
            }
        };

        return map;
    }


    public static async Task DrawCells(Map map)
    {
        try
        {
            var settings = new MagickReadSettings()
            {
                Width = map.Settings.MapWidth,
                Height = map.Settings.MapHeight,
            };
            using var cellsMap = new MagickImage("xc:white", settings);

            var drawables = new Drawables();
            foreach (var feature in map.Input.GeoMap.features)
            {
                foreach (var cell in feature.geometry.coordinates)
                {
                    drawables
                        .DisableStrokeAntialias()
                        .StrokeWidth(2)
                        .StrokeColor(MagickColors.Black)
                        .FillOpacity(new Percentage(0))
                        .Polygon(cell.Select(n => Helper.GeoToPixel(n[0], n[1], map)));
                }
            }

            cellsMap.Draw(drawables);
            await cellsMap.WriteAsync(Helper.GetPath($"{Environment.CurrentDirectory}/cells.png"));
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }
 
    public static async Task DrawProvinces(Map map)
    {
        try
        {
            var settings = new MagickReadSettings()
            {
                Width = map.Settings.MapWidth,
                Height = map.Settings.MapHeight,
            };
            using var cellsMap = new MagickImage("xc:black", settings);

            var drawables = new Drawables();
            foreach (var province in map.Output.Provinces.Skip(1))
            {
                foreach (var cell in province.Cells)
                {
                    drawables
                        .DisableStrokeAntialias()
                        .StrokeColor(province.Color)
                        .FillColor(province.Color)
                        .Polygon(cell.cells.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, map.Settings.MapHeight - (n[1] - map.YOffset) * map.YRatio)));
                }
            }

            cellsMap.Draw(drawables);
            var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "provinces.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await cellsMap.WriteAsync(path);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }

    public static async Task DrawRivers(Map map)
    {
        try
        {
            var settings = new MagickReadSettings()
            {
                Width = map.Settings.MapWidth,
                Height = map.Settings.MapHeight,
            };
            using var cellsMap = new MagickImage("xc:#ff0080", settings);

            var drawables = new Drawables();
            // Draw land
            foreach (var province in map.Output.Provinces.Skip(1).Where(n => !n.IsWater))
            {
                foreach (var cell in province.Cells)
                {
                    drawables
                        .DisableStrokeAntialias()
                        .StrokeColor(MagickColors.White)
                        .FillColor(MagickColors.White)
                        .Polygon(cell.cells.Select(n => Helper.GeoToPixel(n[0], n[1], map)));
                }
            }

            foreach (var river in map.Input.Rivers.features)
            {
                drawables
                    .DisableStrokeAntialias()
                    .StrokeColor(new MagickColor("#00E1FF"))
                    .StrokeWidth(0.5)
                    .Polyline(river.geometry.coordinates.Select(n => Helper.GeoToPixel(n[0], n[1], map)));
            }

            cellsMap.Draw(drawables);
            var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "rivers.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            cellsMap.Settings.SetDefine("png:color-type", "1");

            string[] colormap = [
               "#00FF00",
               "#FF0000",
               "#FFFC00",
               "#00E1FF",
               "#00C8FF",
               "#0096FF",
               "#0064FF",
               "#0000FF",
               "#0000E1",
               "#0000C8",
               "#000096",
               "#000064",
               "#005500",
               "#007D00",
               "#009E00",
               "#18CE00",
               "#FF0080",
               "#FFFFFF",
            ];

            cellsMap.Map(colormap.Select(n => new MagickColor(n)));

            await cellsMap.WriteAsync(path);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }
    public static async Task WriteDefinition(Map map)
    {
        var lines = map.Output.Provinces.Select((n, i) => $"{i};{n.Color.R};{n.Color.G};{n.Color.B};{n.Name};x;");
        var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "definition.csv");
        Helper.EnsureDirectoryExists(path);
        await File.WriteAllLinesAsync(path, lines);
    }

    private static async Task WriteBuildingLocators(Map map)
    {
        var offset = new PointD(0);

        var lines = map.Output.Provinces.Where(n => n.Burg is not null).Select(n =>
        {
            var p = Helper.PixelToFullPixel(n.Burg.x, n.Burg.y, map);
            var str =
$@"        {{
            id = {map.Output.IdToIndex[n.Id]}
            position ={{ {p.X + offset.X:0.000000} {0f:0.000000} {p.Y + offset.Y:0.000000} }}
            rotation ={{ 0.000000 0.000000 0.000000 1.000000 }}
            scale ={{ 1.000000 1.000000 1.000000 }}
        }}";
            return str;
        });
        try
        {
            var file =
$@"game_object_locator={{
	name=""buildings""
	clamp_to_water_level=yes
	render_under_water=no
	generated_content=no
	layer=""building_layer""
	instances={{
{string.Join("\n", lines)}
    }}
}}";
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "building_locators.txt");
            Helper.EnsureDirectoryExists(path);
            await File.WriteAllTextAsync(path, file);
        }
        catch (Exception e)
        {
            Debugger.Break();
            throw;
        }
    }
    private static async Task WriteSiegeLocators(Map map)
    {
        var offset = new PointD(10, -5);
        var lines = map.Output.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var p = Helper.PixelToFullPixel(n.Burg.x, n.Burg.y, map);

            var str =
$@"        {{
            id = {map.Output.IdToIndex[n.Id]}
            position ={{ {p.X + offset.X:0.000000} {0f:0.000000} {p.Y + offset.Y:0.000000} }}
            rotation ={{ 0.000000 0.000000 0.000000 1.000000 }}
            scale ={{ 1.000000 1.000000 1.000000 }}
        }}";
            return str;
        });
        try
        {
            var file =
$@"game_object_locator={{
	name=""siege""
	clamp_to_water_level=no
	render_under_water=no
	generated_content=no
	layer=""unit_layer""
	instances={{
{string.Join("\n", lines)}
    }}
}}";
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "siege_locators.txt");
            Helper.EnsureDirectoryExists(path);
            await File.WriteAllTextAsync(path, file);
        }
        catch (Exception e)
        {
            Debugger.Break();
            throw;
        }
    }
    private static async Task WriteCombatLocators(Map map)
    {
        var offset = new PointD(0, 10);
        var lines = map.Output.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var p = Helper.PixelToFullPixel(n.Burg.x, n.Burg.y, map);

            var str =
$@"        {{
            id = {map.Output.IdToIndex[n.Id]}
            position ={{ {p.X + offset.X:0.000000} {0f:0.000000} {p.Y + offset.Y:0.000000} }}
            rotation ={{ 0.000000 0.000000 0.000000 1.000000 }}
            scale ={{ 1.000000 1.000000 1.000000 }}
        }}";
            return str;
        });
        try
        {
            var file =
$@"game_object_locator={{
	name=""combat""
	clamp_to_water_level=yes
	render_under_water=no
	generated_content=no
	layer=""unit_layer""
	instances={{
{string.Join("\n", lines)}
    }}
}}";
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "combat_locators.txt");
            Helper.EnsureDirectoryExists(path);
            await File.WriteAllTextAsync(path, file);
        }
        catch (Exception e)
        {
            Debugger.Break();
            throw;
        }
    }
    private static async Task WritePlayerStackLocators(Map map)
    {
        var offset = new PointD(-10, -5);
        var lines = map.Output.Provinces.Skip(1).Select((n, i) =>
        {
            var p = new PointD();
            if (n.Burg is null)
            {
                var maxLon = n.Cells.SelectMany(n => n.cells).MaxBy(n => n[0])[0];
                var minLon = n.Cells.SelectMany(n => n.cells).MinBy(n => n[0])[0];

                var maxLat = n.Cells.SelectMany(n => n.cells).MaxBy(n => n[1])[1];
                var minLat = n.Cells.SelectMany(n => n.cells).MinBy(n => n[1])[1];

                p = Helper.GeoToPixelCrutch((maxLon + minLon) / 2, (maxLat + minLat) / 2, map);
            }
            else
            {
                p = Helper.PixelToFullPixel(n.Burg.x, n.Burg.y, map);
            }

            var str =
$@"        {{
            id = {map.Output.IdToIndex[n.Id]}
            position ={{ {p.X + offset.X:0.000000} {0f:0.000000} {p.Y + offset.Y:0.000000} }}
            rotation ={{ 0.000000 0.000000 0.000000 1.000000 }}
            scale ={{ 1.000000 1.000000 1.000000 }}
        }}";
            return str;
        });
        try
        {
            var file =
$@"game_object_locator={{
	name=""unit_stack_player_owned""
	clamp_to_water_level=yes
	render_under_water=no
	generated_content=no
	layer=""unit_layer""
	instances={{
{string.Join("\n", lines)}
    }}
}}";
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "player_stack_locators.txt");
            Helper.EnsureDirectoryExists(path);
            await File.WriteAllTextAsync(path, file);
        }
        catch (Exception e)
        {
            Debugger.Break();
            throw;
        }
    }
    public static async Task WriteLocators(Map map)
    {
        await WriteBuildingLocators(map);
        await WriteSiegeLocators(map);
        await WriteCombatLocators(map);
        await WritePlayerStackLocators(map);
    }

    public static async Task WriteDefault(Map map)
    {
        var waterProvinces = map.Output.Provinces.Select((n, i) => (n, i)).Where(n => n.n.IsWater).Select(n => n.i);
        var file = $@"#max_provinces = 1466
definitions = ""definition.csv""
provinces = ""provinces.png""
#positions = ""positions.txt""
rivers = ""rivers.png""
#terrain_definition = ""terrain.txt""
topology = ""heightmap.heightmap""
#tree_definition = ""trees.bmp""
continent = ""continent.txt""
adjacencies = ""adjacencies.csv""
#climate = ""climate.txt""
island_region = ""island_region.txt""
seasons = ""seasons.txt""

#############
# SEA ZONES
#############

sea_zones = LIST {{ {string.Join(" ", waterProvinces)} }}

###############
# MAJOR RIVERS
###############

########
# LAKES
########

#####################
# IMPASSABLE TERRAIN
#####################
# Can be colored by whoever owns the most of the province's neighbours.
# Blocks unit movement.

############
# WASTELAND
############
# These are actually supposed to be Wasteland:
# Cannot be colored. Blocks unit movement, used for things like Sahara desert. 

# IMPASSABLE SEA ZONES
# These sea provinces cannot be crossed/sailed

# UNUSED PROVINCES
# These provinces cause issues because they are passable land, but not part of any title.
# They are probably not visible anywhere on the map, so feel free to reuse them (after double checking that they are actually missing).
";

        var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "default.map");
        Helper.EnsureDirectoryExists(path);
        await File.WriteAllTextAsync(path, file);
    }

    public static async Task DrawFlatMap(Map map)
    {
        try
        {
            XmlNode? GetNode(string attribute) => map.Input.XmlMap.SelectSingleNode($"//*[{attribute}]");

            void Remove(string attribute)
            {
                var node = map.Input.XmlMap.SelectSingleNode($"//*[{attribute}]");
                node?.ParentNode?.RemoveChild(node);
            }

            var terrain = GetNode("@id='svgFlatMap'");

            {
                // remove all title elements. They break svg.
                var titleElements = map.Input.XmlMap.SelectNodes("//title");
                foreach (XmlElement item in titleElements)
                {
                    item.ParentNode.RemoveChild(item);
                }
            }

            var xml = new XmlDocument();
            xml.LoadXml(terrain.OuterXml);
            var svg = SvgDocument.Open(xml);
            //File.WriteAllText("flatMap.svg", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>" + xml.OuterXml);

            var bitmap = svg.Draw(map.Settings.MapWidth, map.Settings.MapHeight);
            bitmap.Save("flatmap.png", ImageFormat.Png);

            using var flatmap = new MagickImage("flatmap.png");
            await flatmap.WriteAsync(Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "flatmap.dds"), MagickFormat.Dds);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }

    }


    // Town Biomes
    public static async Task WriteTerrain(Map map)
    {
        try
        {
            var provinceBiomes = map.Output.Provinces
                .Select((n, i) => (n, i))
                .Skip(1)
                .Where(n => !n.n.IsWater && n.n.Cells.Any())
                .Select(n =>
                    {
                        var hd = Helper.HeightDifference(n.n);

                        return new
                        {
                            ProvinceId = n.i,
                            PrimaryBiome = n.n.Cells.Select(m => m.biome).Max(),
                            HeightDifference = (int)hd,
                        };
                    })
                .Select(n => $"{n.ProvinceId}={Helper.GetProvinceBiomeName(n.PrimaryBiome, n.HeightDifference)}").ToArray();

            var file = $@"default=plains
{string.Join("\n", provinceBiomes)}";
            var path = Helper.GetPath(Settings.OutputDirectory, "common", "province_terrain", "00_province_terrain.txt");
            Helper.EnsureDirectoryExists(path);
            await File.WriteAllTextAsync(path, file);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }

    }
    private static async Task WriteMask(IEnumerable<Cell> cells, Map map, string filename)
    {
        try
        {
            using var cellsMap = new MagickImage(Helper.GetPath(SettingsManager.ExecutablePath, "template_mask.png"));

            var drawables = new Drawables();
            foreach (var cell in cells.Select(n => n.cells))
            {
                drawables
                    .DisableStrokeAntialias()
                    .StrokeColor(MagickColors.White)
                    .FillColor(MagickColors.White)
                    .Polygon(cell.Select(n => Helper.GeoToPixel(n[0], n[1], map)));
            }

            cellsMap.Draw(drawables);

            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", $"{filename}.png");
            Helper.EnsureDirectoryExists(path);
            await cellsMap.WriteAsync(path, MagickFormat.Png00);

            using var file = await Image.LoadAsync(path);
            file.Mutate(n => n.GaussianBlur(15));
            file.Save(path);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }
    public static async Task WriteMasks(Map map)
    {
        var nonWaterProvinceCells = map.Output.Provinces
            .Skip(1)
            .Where(n => !n.IsWater && n.Cells.Any())
            .SelectMany(n => n.Cells)
            .ToArray();

        var provinceBiomes = map.Output.Provinces
            .Skip(1)
            .Where(n => !n.IsWater && n.Cells.Any())
            .Select(n =>
            {
                var primaryBiome = n.Cells.Select(m => m.biome).Max();
                var heightDifference = (int)Helper.HeightDifference(n);
                return new
                {
                    Province = n,
                    Biome = Helper.GetProvinceBiomeName(primaryBiome, heightDifference)
                };
            }).ToArray();

        // Remove all masks
        {
            var templatePath = Helper.GetPath(SettingsManager.ExecutablePath, "template_mask.png");
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain");
            Helper.EnsureDirectoryExists(path);
            foreach (var fileName in Directory.EnumerateFiles(path).Where(n => n.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
            {
                File.Delete(fileName);
                File.Copy(templatePath, fileName);
            }
        }

        //// drylands
        //await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "drylands"), map, "drylands_01_mask"),
        // taiga
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is var b && (b == "taiga" || b == "drylands" && Helper.IsCellLowMountains(n.height) || b == "drylands" && Helper.IsCellMountains(n.height))), map, "forest_pine_01_mask");
        // plains
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "plains"), map, "plains_01_mask");
        // farmlands
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "farmlands"), map, "farmland_01_mask");
        // Desert
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "desert" && !Helper.IsCellMountains(n.height) && !Helper.IsCellHighMountains(n.height)), map, "desert_01_mask");
        // desert_mountains
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "desert" && Helper.IsCellMountains(n.height)), map, "mountain_02_desert_mask");
        // oasis
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "oasis"), map, "oasis_mask");
        // hills
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.IsCellHills(n.biome, n.height)), map, "hills_01_mask");
        // low mountains
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellLowMountains(n.height)), map, "mountain_02_mask");
        // mountains
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellMountains(n.height) || Helper.IsCellHighMountains(n.height)), map, "mountain_02_snow_mask");
        // HighMountains
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellHighMountains(n.height)), map, "mountain_02_c_snow_mask");
        // jungle
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "jungle"), map, "forest_jungle_01_mask");
        // forest
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "forest"), map, "forest_leaf_01_mask");
        // wetlands
        await WriteMask(provinceBiomes.Where(n => n.Biome == "wetlands").SelectMany(n => n.Province.Cells).Where(n => Helper.MapBiome(n.biome) == "floodplains"), map, "wetlands_02_mask");
        // steppe
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "steppe"), map, "wetlands_02_mask");
        // floodplains
        await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "floodplains"), map, "wetlands_02_mask");
    }

    public static async Task WriteGraphics()
    {
        var path = Helper.GetPath(Settings.OutputDirectory, "common", "defines", "graphic", "00_graphics.txt");
        Helper.EnsureDirectoryExists(path);
        File.Copy(Helper.GetPath(SettingsManager.ExecutablePath, "00_graphics.txt"), path, true);
    }
    public static async Task WriteDefines(Map map)
    {
        //var maxElevation = 51;
        var maxElevation = 255;
        var file = $@"NJominiMap = {{
	WORLD_EXTENTS_X = {map.Settings.MapWidth - 1}
	WORLD_EXTENTS_Y = {maxElevation}
	WORLD_EXTENTS_Z = {map.Settings.MapHeight - 1}
	WATERLEVEL = 3.8
}}";
        var path = Helper.GetPath(Settings.OutputDirectory, "common", "defines", "00_defines.txt");
        Helper.EnsureDirectoryExists(path);
        await File.WriteAllTextAsync(path, file);
    }

    private static string[] GetOriginalCultures()
    {
        var path = Helper.GetPath(Settings.Instance.Ck3Directory, "common", "culture", "cultures");
        var cultures = Directory.EnumerateFiles(path)
            .Where(n => n.EndsWith(".txt"))
            .SelectMany(n =>
            {
                var file = File.ReadAllText(n);
                var parsedFile = CK3FileReader.Read(file);
                return parsedFile.Keys.ToArray();
            })
            .Where(n => !string.Equals(n, "values", StringComparison.InvariantCultureIgnoreCase))
            .Distinct()
            .ToArray();

        return cultures;
    }
    private static async Task<(Dictionary<int, int> baronyCultures, string[] toOriginalCultureName)> GetCultures(Map map)
    {
        var originalCultureNames = GetOriginalCultures();
        var baronyCultures = map.Output.Empires
            .SelectMany(n => n.kingdoms)
            .SelectMany(n => n.duchies)
            .SelectMany(n => n.counties)
            .SelectMany(n => n.baronies)
            .ToDictionary(n => n.id, n => n.province.Cells.Select(m => m.culture).Max());

        var totalCultures = map.Input.JsonMap.pack.cultures.Length;
        if (totalCultures > originalCultureNames.Length)
        {
            // Generated too many cultures.
            Debugger.Break();
            throw new ArgumentException("Too many cultures were generated.");
        }
        var toOriginalCultureName = new string[totalCultures + 1];
        //// Consistent cultures for debugging
        //{
        //    // cultureId starts from 1
        //    for (int i = 0; i < totalCultures + 1; i++)
        //    {
        //        ToOriginalCultureName[i] = originalCultureNames[i];
        //    }
        //}
        // Randomized cultures
        {
            var indices = Enumerable.Range(0, originalCultureNames.Length).ToList();
            var r = new Random(1);

            // cultureId starts from 1
            for (int i = 0; i < totalCultures + 1; i++)
            {
                var index = r.Next(indices.Count);
                toOriginalCultureName[i] = originalCultureNames[indices[index]];
                indices.RemoveAt(index);
            }
        }

        return (baronyCultures, toOriginalCultureName);
    }
    private static async Task<(Dictionary<int, int> baronyReligions, string[] toOriginalReligionName)> GetReligions(Map map)
    {
        var originalReligionNames = (await ConfigReader.GetCK3Religions(map.Settings)).SelectMany(n => n.faiths).Select(n => n.name).ToArray();
        var baronyReligions = map.Output.Empires
            .SelectMany(n => n.kingdoms)
            .SelectMany(n => n.duchies)
            .SelectMany(n => n.counties)
            .SelectMany(n => n.baronies)
            .ToDictionary(n => n.id, n => n.province.Cells.Select(m => m.religion).Max());

        var totalReligions = map.Input.JsonMap.pack.religions.Length;
        if (totalReligions > originalReligionNames.Length)
        {
            // Generated too many cultures.
            Debugger.Break();
            throw new ArgumentException("Too many religions were generated.");
        }
        var toOriginalReligionName = new string[totalReligions + 1];
        //// Consistent cultures for debugging
        //{
        //    // cultureId starts from 1
        //    for (int i = 0; i < totalCultures + 1; i++)
        //    {
        //        ToOriginalCultureName[i] = originalCultureNames[i];
        //    }
        //}
        // Randomized cultures
        {
            var indices = Enumerable.Range(0, originalReligionNames.Length).ToList();
            var r = new Random(1);

            // cultureId starts from 1
            for (int i = 0; i < totalReligions + 1; i++)
            {
                var index = r.Next(indices.Count);
                toOriginalReligionName[i] = originalReligionNames[indices[index]];
                indices.RemoveAt(index);
            }
        }

        return (baronyReligions, toOriginalReligionName);
    }
    public static async Task<string[]> ApplyCultureReligion(Map map)
    {
        var (baronyCultures, toOriginalCultureName) = await GetCultures(map);
        var (baronyReligions, toOriginalReligionName) = await GetReligions(map);

        foreach (var empire in map.Output.Empires)
        {
            foreach (var kingdom in empire.kingdoms)
            {
                foreach (var duchy in kingdom.duchies)
                {
                    foreach (var county in duchy.counties)
                    {
                        foreach (var barony in county.baronies)
                        {
                            barony.Culture = toOriginalCultureName[baronyCultures[barony.id]];
                            barony.Religion = toOriginalReligionName[baronyReligions[barony.id]];
                        }
                        county.Culture = county.baronies[0].Culture;
                        county.Religion = county.baronies[0].Religion;
                    }
                    duchy.Culture = duchy.counties[0].Culture;
                    duchy.Religion = duchy.counties[0].Religion;
                }
                kingdom.Culture = kingdom.duchies[0].Culture;
                kingdom.Religion = kingdom.duchies[0].Religion;
            }
            empire.Culture = empire.kingdoms[0].Culture;
            empire.Religion = empire.kingdoms[0].Religion;
        }

        return toOriginalReligionName;
    }
    public static async Task WriteHistoryProvinces(Map map)
    {
        foreach (var empire in map.Output.Empires)
        {
            foreach (var kingdom in empire.kingdoms)
            {
                var baronies = kingdom.duchies
                    .SelectMany(n => n.counties)
                    .SelectMany(n => n.baronies)
                    .Select(n =>
                    {
                        var str = $@"{map.Output.IdToIndex[n.province.Id]} = {{
    culture = {n.Culture}
    religion = {n.Religion}
    holding = auto
}}";
                        return str;
                    })
                    .ToArray();

                var file = string.Join('\n', baronies);
                var path = Helper.GetPath(Settings.OutputDirectory, "history", "provinces", $"k_{kingdom.id}.txt");
                Helper.EnsureDirectoryExists(path);
                await File.WriteAllTextAsync(path, file);
            }
        }

        // Make original culture history empty
        // Otherwise it will override newly created culture
        var originalProvincesPath = Helper.GetPath(map.Settings.Ck3Directory, "history", "provinces");
        var provincesPath = Helper.GetPath(Settings.OutputDirectory, "history", "provinces");
        foreach (var p in Directory.EnumerateFiles(originalProvincesPath))
        {
            File.WriteAllText(provincesPath + Path.GetFileName(p), "");
        }
    }
    public static async Task CopyOriginalReligions(Map map)
    {
        try
        {
            // Delete religion file from Total conversion sandbox mod.
            File.Delete(Helper.GetPath(Settings.OutputDirectory, "common", "religion", "religions", "01_vanilla.txt"));
        }
        catch
        {
            // Do nothing.
        }

        var religionsPath = Helper.GetPath(map.Settings.Ck3Directory, "common", "religion", "religions");
        FileSystem.CopyDirectory(religionsPath, Helper.GetPath(Settings.OutputDirectory, "common", "religion", "religions"), true);
    }
    // Maps original holy sites to newly created provinces.
    public static async Task WriteHolySites(Map map, string[] pickedFaiths)
    {
        var originalFaiths = (await ConfigReader.GetCK3Religions(map.Settings)).SelectMany(n => n.faiths).Where(n => pickedFaiths.Contains(n.name)).ToArray();
        var originalHolySites = await ConfigReader.GetCK3HolySites(map.Settings);

        var pickedHolySites = pickedFaiths
            .SelectMany(n => originalFaiths.First(m => m.name == n).holySites.Select(m => (name: m, holySite: originalHolySites[m])))
            .ToArray();

        var counties = map.Output.Empires.SelectMany(n => n.kingdoms).SelectMany(n => n.duchies).SelectMany(n => n.counties).ToArray();

        var rnd = new Random();
        var mappedHolySites = pickedHolySites.Select(n =>
        {
            var county = counties[rnd.Next(0, counties.Length)];
            int? barony = string.IsNullOrWhiteSpace(n.holySite.barony) ? null : county.baronies[rnd.Next(0, county.baronies.Count)].id;

            var baronyStr = barony > 0 ? $"barony = b_{barony}" : null;
            var isActiveStr = n.holySite?.is_active == "no" ? $"is_active = no" : null;
            var flagStr = string.IsNullOrEmpty(n.holySite?.flag) ? null : $"flag = {n.holySite.flag}";
            var characterModifierStr = string.Join("\n", n.holySite?.character_modifier?.Select(n => $"     {n.Key} = {n.Value}") ?? new string[0]);

            return $@"{n.name} = {{
    county = c_{county.id}
    {baronyStr}
    {isActiveStr}
    {flagStr}
    character_modifier = {{
{characterModifierStr}
    }}
}}";
        });

        var file = string.Join('\n', mappedHolySites);
        var path = Helper.GetPath(Settings.OutputDirectory, "common", "religion", "holy_sites", "00_holy_sites.txt");
        Helper.EnsureDirectoryExists(path);
        await File.WriteAllTextAsync(path, file);
    }
}
