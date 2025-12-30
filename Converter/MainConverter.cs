using ImageMagick;
using Microsoft.VisualBasic.FileIO;
using SixLabors.ImageSharp;
using Svg;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;

namespace Converter;

// These are needed for AOT compilation.
[JsonSerializable(typeof(GeoMap))]
public partial class GeoMapJsonContext : JsonSerializerContext {}

[JsonSerializable(typeof(PackProvince))]
[JsonSerializable(typeof(JsonMap))]
public partial class JsonMapJsonContext : JsonSerializerContext {}

public static class MainConverter
{
    public static async Task<XmlDocument> LoadXml()
    {
        try
        {
            var unescapedFile = File.ReadAllText(Settings.Instance.InputXmlPath);
            unescapedFile = unescapedFile.Replace("&amp;quot;", "\"");
            unescapedFile = new Regex(@"xmlns[^\s]+""").Replace(unescapedFile, "");
            // remove xlink namespace prefixes
            unescapedFile = new Regex(@"xlink:").Replace(unescapedFile, "");

            try
            {
                var file = new XmlDocument();
                file.LoadXml(unescapedFile);
                return file;
            }
            catch (Exception ex)
            {
                Debugger.Break();
                throw;
            }
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
                // Skip deleted provinces
                if (province is null)
                {
                    continue;
                }

                var cells = province.Cells;
                var cellsToRemove = new List<Cell>();
                foreach (var cell in cells)
                {
                    if (!cells.Any(m => cell.neighbors.Contains(m.id)))
                    {
                        var nonWaterNeighborProvince = nonWaterProvinces.FirstOrDefault(p =>
                        {
                            return p != null && p.Cells.Any(c => cell.neighbors.Contains(c.id));
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
                MyConsole.Info($"Water provinces created: {provinces.Count}...");
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
        var provinces = new Province[jsonmap.pack.provinces.Length + waterProvinces.Count];

        // If provinces are deleted we should preserve their index in the array.
        // Deleted provinces will not take part in title or other generation.
        var maxProvinceId = jsonmap.pack.provinces.Max(n => n.i);

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
            foreach (var (id, province) in provinceCells.Skip(1))
            {
                var color = GetColor(id, provinces.Length);
                provinces[id] = province;

                try
                {
                    province.Color = color;
                    province.Name = jsonmap.pack.provinces[id].name;
                    province.Id = jsonmap.pack.provinces[id].i;

                    // If the burg was deleted then skip it.
                    province.Burg = jsonmap.pack.burgs.FirstOrDefault(n => n.i == jsonmap.pack.provinces[id].burg, null);
                }
                catch (Exception ex)
                {
                    Debugger.Break();
                    throw;
                }

                var cellIds = province.Cells.Select(n => n.id).ToHashSet();
                neighborCellIds[id] = province.Cells.SelectMany(n => n.neighbors.Where(m => !cellIds.Contains(m))).ToArray();
            }

            // Create sea provinces
            for (int i = 0; i < waterProvinces.Count; i++)
            {
                var province = provinces[maxProvinceId + i] = waterProvinces[i];
                province.Color = GetColor(maxProvinceId + i, provinces.Length);
                province.Name = "sea";
                province.Id = maxProvinceId + i;
                province.IsWater = true;
            }

            // Populate neighbors
            for (int i = 0; i < maxProvinceId; i++)
            {
                var neighbors = new HashSet<Province>();

                if (neighborCellIds.TryGetValue(i, out var cellIds))
                {
                    var processedNeighbors = new HashSet<int>();
                    foreach (var cid in cellIds)
                    {
                        if (processedNeighbors.Contains(cid)) continue;

                        // Skip deleted provinces
                        foreach (var p in provinces.Where(n => n != null && n.Id != 0 && !n.IsWater && n.StateId == provinces[i].StateId && n.Cells.Any(m => m.id == cid)))
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
        GeoMap? geoMap = null;
        try
        {
            geoMap = JsonSerializer.Deserialize(geoMapXml, GeoMapJsonContext.Default.GeoMap);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }

        var jsonMapXml = GetNode("@id='json'")!.InnerXml;
        JsonMap? jsonMap = null;
        try
        {
            jsonMap = JsonSerializer.Deserialize(jsonMapXml, JsonMapJsonContext.Default.JsonMap);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }

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
                Provinces = provinces.Where(n => n is not null).ToArray(),
                IdToIndex = provinces.Where(n => n is not null).Select((n, i) => (n, i)).ToDictionary(n => n.n.Id, n => n.i),
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
                Width = (int)map.Settings.MapWidth,
                Height = (int)map.Settings.MapHeight,
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
            var str = $$"""
                {
                    id={{map.Output.IdToIndex[n.Id]}}
                    position={ {{p.X + offset.X:0.000000}} 0.000000 {{p.Y + offset.Y:0.000000}} }
                    rotation={ 0.000000 0.000000 0.000000 1.000000 }
                    scale={ 1.000000 1.000000 1.000000 }
                }
                """;
            return str;
        });
        try
        {
            var file = $$"""
                game_object_locator={
                    name="buildings"
                    clamp_to_water_level=yes
                    render_under_water=no
                    generated_content=no
                    layer="building_layer"
                    instances={
                        {{string.Join("\n", lines)}}
                    }
                }
                """;
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
            var file = $$"""
                game_object_locator={
                    name="siege"
                    clamp_to_water_level=yes
                    render_under_water=no
                    generated_content=no
                    layer="unit_layer"
                    instances={
                        {{string.Join("\n", lines)}}
                    }
                }
                """;
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
            var file = $$"""
                game_object_locator={
                    name="combat"
                    clamp_to_water_level=yes
                    render_under_water=no
                    generated_content=no
                    layer="unit_layer"
                    instances={
                        {{string.Join("\n", lines)}}
                    }
                }
                """;
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
            var file = $$"""
                game_object_locator={
                    name="unit_stack_player_owned"
                    clamp_to_water_level=yes
                    render_under_water=no
                    generated_content=no
                    layer="unit_layer"
                    instances={
                        {{string.Join("\n", lines)}}
                    }
                }
                """;
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
    private static async Task WriteMapTableCe1Locators(Map map)
    {
        var file = """
            object={
            	name="ce1_tabletop"
            	clamp_to_water_level=yes
            	render_pass=MapUnderTerrain
            	generated_content=no
            	layer="map_table_layer_ce1"
            	entity="ce1_tabletop_entity"
            	count=1
            	transform="3100 -20 2048 0 0 0 0 5 5 5"

            }
            object={
            	name="ce1_tabletop_cloth"
            	clamp_to_water_level=yes
            	render_pass=MapUnderTerrain
            	generated_content=no
            	layer="map_table_layer_ce1"
            	entity="ce1_tabletop_tablecloth_entity"
            	count=1
            	transform="3100 -20 2048 0 0 0 0 5 5 5"

            }
            object={
            	name="ce1_tabletop_props_01"
            	clamp_to_water_level=yes
            	render_pass=MapUnderTerrain
            	generated_content=no
            	layer="map_table_layer_ce1"
            	entity="ce1_tabletop_props_entity"
            	count=1
            	transform="3100 1 2048 0 0 0 0 5 5 5"

            }
            object={
            	name="ce1_tabletop_ground_props_01"
            	clamp_to_water_level=yes
            	render_pass=MapUnderTerrain
            	generated_content=no
            	layer="map_table_layer_ce1"
            	entity="ce1_tabletop_groundprops_entity"
            	count=1
            	transform="3100 1 2048 0 0 0 0 5 5 5"

            }
            object={
            	name="ce1_tabletop_floor_01"
            	clamp_to_water_level=yes
            	render_pass=MapUnderTerrain
            	generated_content=no
            	layer="map_table_layer_ce1"
            	entity="ce1_tabletop_floor_entity"
            	count=1
            	transform="3100 1 2048 0 0 0 0 5 5 5"

            }
            object={
            	name="ce1_tabletop_candles_01"
            	clamp_to_water_level=yes
            	render_pass=MapUnderTerrain
            	generated_content=no
            	layer="map_table_layer_ce1"
            	entity="ce1_tabletop_candles_entity"
            	count=1
            	transform="3100 1 2048 0 0 0 0 5 5 5"
            }
            """;
        File.WriteAllText(Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "map_table_ce1.txt"), file, new UTF8Encoding(true));
    }
    private static async Task WriteMapTableTgpLocators(Map map)
    {
        var file = """
            object={
            	name="tgp_tabletop"
            	render_pass=MapUnderTerrain
            	clamp_to_water_level=yes
            	generated_content=no
            	layer="map_table_layer_tgp"
            	entity="tgp_tabletop_01_a_entity"
            	count=1
            	transform="4450.000000 -12.000000 2450.000000 0.000000 0.000000 0.000000 0.000000 1.000000 1.000000 1.000000
            "}
            object={
            	name="tgp_tabletop_floor"
            	render_pass=MapUnderTerrain
            	clamp_to_water_level=yes
            	generated_content=no
            	layer="map_table_layer_tgp"
            	entity="tgp_tabletop_floor_01_a_entity"
            	count=1
            	transform="4200.000000 1.000000 2700.000000 0.000000 0.000000 0.000000 0.000000 1.000000 1.000000 1.000000
            "}
            
            """;
        File.WriteAllText(Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "map_table_tgp.txt"), file, new UTF8Encoding(true));
    }
    private static async Task WriteMapTableWesternLocators(Map map)
    {
        var file = """
            object={
            	name="western_tabletop"
            	clamp_to_water_level=yes
            	render_pass=MapUnderTerrain
            	generated_content=no
            	layer="map_table_layer_western"
            	entity="tabletop_west_basic_entity"
            	count=1
            	transform="3100 -20 2048 0 0 0 0 5 5 5"

            }
            object={
            	name="western_tabletop_cloth"
            	clamp_to_water_level=yes
            	render_pass=MapUnderTerrain
            	generated_content=no
            	layer="map_table_layer_western"
            	entity="tabletop_west_basic_tablecloth_entity"
            	count=1
            	transform="3100 -20 2048 0 0 0 0 5 5 5"

            }
            object={
            	name="western_tabletop_candles_01"
            	clamp_to_water_level=yes
            	render_pass=MapUnderTerrain
            	generated_content=no
            	layer="map_table_layer_western"
            	entity="tabletop_west_basic_candles_entity"
            	count=1
            	transform="3100 1 2048 0 0 0 0 5 5 5"

            }
            object={
            	name="western_tabletop_props_01"
            	clamp_to_water_level=yes
            	render_pass=MapUnderTerrain
            	generated_content=no
            	layer="map_table_layer_western"
            	entity="tabletop_west_basic_props_entity"
            	count=1
            	transform="3100 1 2048 0 0 0 0 5 5 5"

            }
            """;
        File.WriteAllText(Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "map_table_western.txt"), file, new UTF8Encoding(true));
    }
    private static async Task WriteMapTableEp3Locators(Map map)
    {
        var file = """
            object={
            	name="ep3_tabletop"
            	render_pass=MapUnderTerrain
            	clamp_to_water_level=yes
            	generated_content=no
            	layer="map_table_layer_ep3"
            	entity="ep3_tabletop_entity"
            	count=1
            	transform="3100.000000 -20.000000 2048.000000 0.000000 0.000000 0.000000 0.000000 5.000000 5.000000 5.000000
            "}
            object={
            	name="ep3_tabletop_floor"
            	render_pass=MapUnderTerrain
            	clamp_to_water_level=yes
            	generated_content=no
            	layer="map_table_layer_ep3"
            	entity="ep3_tabletop_floor_entity"
            	count=1
            	transform="3100.000000 -20.000000 2048.000000 0.000000 0.000000 0.000000 0.000000 5.000000 5.000000 5.000000
            "}
            object={
            	name="tabletop_props"
            	render_pass=MapUnderTerrain
            	clamp_to_water_level=yes
            	generated_content=no
            	layer="map_table_layer_ep3"
            	entity="tabletop_props_entity"
            	count=1
            	transform="3100.000000 1.000000 2048.000000 0.000000 0.000000 0.000000 0.000000 5.000000 5.000000 5.000000
            "}
            """;
        File.WriteAllText(Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "map_table_ep3.txt"), file, new UTF8Encoding(true));
    }

    public static Task WriteLocators(Map map)
    {
        return Task.WhenAll([
            WriteBuildingLocators(map),
            WriteSiegeLocators(map),
            WriteCombatLocators(map),
            WritePlayerStackLocators(map),

            // Move the table props to not obstruct the map
            WriteMapTableCe1Locators(map),
            WriteMapTableTgpLocators(map),
            WriteMapTableWesternLocators(map),
            WriteMapTableEp3Locators(map),
        ]);
    }

    public static async Task WriteDefault(Map map)
    {
        var waterProvinces = map.Output.Provinces!.Select((n, i) => (n, i)).Where(n => n.n.IsWater).Select(n => n.i).ToArray();
        var file = $@"#max_provinces = {map.Output.Provinces!.Length - waterProvinces.Length}
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

            var fog = map.Input.XmlMap.SelectSingleNode($"//*[@id='fog']/rect") as XmlElement;
            (terrain as XmlElement).SetAttribute("viewBox", $"0 0 {fog.GetAttribute("width")} {fog.GetAttribute("height")}");

            var svg = SvgDocument.FromSvg<SvgDocument>(terrain.OuterXml);
            var img = svg.ToImage(map.Settings.MapWidth, map.Settings.MapHeight);
            var base64 = img.ToBase64String(SixLabors.ImageSharp.Formats.Png.PngFormat.Instance);
            var magickImage = MagickImage.FromBase64(base64.Split(',')[1]);
            //magickImage.Write("terrain.dds", MagickFormat.Dds);

            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "flat_maps", "flatmap.dds");
            Helper.EnsureDirectoryExists(path);
            magickImage.Write(path, MagickFormat.Dds);

            File.Copy(path, Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "flat_maps", "flatmap_tgp.dds"), true);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }

    }

    public static async Task WritePdxterrain()
    {
        var offset = 10;
        var file = $$"""
                        Includes = {
            	"cw/pdxterrain.fxh"
            	"cw/heightmap.fxh"
            	"cw/shadow.fxh"
            	"cw/utility.fxh"
            	"cw/camera.fxh"
            	"cw/lighting_util.fxh"
            	"cw/lighting.fxh"
            	"jomini/jomini_fog.fxh"
            	"jomini/map_lighting.fxh"
            	"jomini/jomini_fog_of_war.fxh"
            	"jomini/jomini_water.fxh"
            	"standardfuncsgfx.fxh"
            	"bordercolor.fxh"
            	"lowspec.fxh"
            	"legend.fxh"
            	"dynamic_masks.fxh"
            	"disease.fxh"
            	"shadow_tint.fxh"
            	"clouds.fxh"
            	"province_effects.fxh"
            	"paper_transition.fxh"
            	"utility_game.fxh"
            }

            VertexStruct VS_OUTPUT_PDX_TERRAIN
            {
            	float4 Position			: PDX_POSITION;
            	float3 WorldSpacePos	: TEXCOORD1;
            	float4 ShadowProj		: TEXCOORD2;
            };

            VertexStruct VS_OUTPUT_PDX_TERRAIN_LOW_SPEC
            {
            	float4 Position			: PDX_POSITION;
            	float3 WorldSpacePos	: TEXCOORD1;
            	float4 ShadowProj		: TEXCOORD2;
            	float3 DetailDiffuse	: TEXCOORD3;
            	float4 DetailMaterial	: TEXCOORD4;
            	float3 ColorMap			: TEXCOORD5;
            	float3 FlatMap			: TEXCOORD6;
            	float3 Normal			: TEXCOORD7;
            };

            # Limited JominiEnvironment data to get nicer transitions between the Flatmap lighting and Terrain lighting
            # Only used in terrain shader while lerping between flatmap and terrain.
            ConstantBuffer( FlatMapLerpEnvironment )
            {
            	float	FlatMapLerpCubemapIntensity;
            	float3	FlatMapLerpSunDiffuse;
            	float	FlatMapLerpSunIntensity;
            	float4x4 FlatMapLerpCubemapYRotation;
            };

            VertexShader =
            {
            	TextureSampler DetailTextures
            	{
            		Ref = PdxTerrainTextures0
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Wrap"
            		SampleModeV = "Wrap"
            		type = "2darray"
            	}
            	TextureSampler NormalTextures
            	{
            		Ref = PdxTerrainTextures1
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Wrap"
            		SampleModeV = "Wrap"
            		type = "2darray"
            	}
            	TextureSampler MaterialTextures
            	{
            		Ref = PdxTerrainTextures2
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Wrap"
            		SampleModeV = "Wrap"
            		type = "2darray"
            	}
            	TextureSampler DetailIndexTexture
            	{
            		Ref = PdxTerrainTextures3
            		MagFilter = "Point"
            		MinFilter = "Point"
            		MipFilter = "Point"
            		SampleModeU = "Clamp"
            		SampleModeV = "Clamp"
            	}
            	TextureSampler DetailMaskTexture
            	{
            		Ref = PdxTerrainTextures4
            		MagFilter = "Point"
            		MinFilter = "Point"
            		MipFilter = "Point"
            		SampleModeU = "Clamp"
            		SampleModeV = "Clamp"
            	}
            	TextureSampler ColorTexture
            	{
            		Ref = PdxTerrainColorMap
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Clamp"
            		SampleModeV = "Clamp"
            	}
            	TextureSampler FlatMapTexture
            	{
            		Ref = TerrainFlatMap
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Clamp"
            		SampleModeV = "Clamp"
            	}

            	Code
            	[[
            		VS_OUTPUT_PDX_TERRAIN TerrainVertex( float2 WithinNodePos, float2 NodeOffset, float NodeScale, float2 LodDirection, float LodLerpFactor )
            		{
            			STerrainVertex Vertex = CalcTerrainVertex( WithinNodePos, NodeOffset, NodeScale, LodDirection, LodLerpFactor );

            			#ifdef TERRAIN_FLAT_MAP_LERP
            				Vertex.WorldSpacePos.y = lerp( Vertex.WorldSpacePos.y, FlatMapHeight + 10, FlatMapLerp );
            			#endif
            			#ifdef TERRAIN_FLAT_MAP
            				Vertex.WorldSpacePos.y = FlatMapHeight + 10;
            			#endif

            			VS_OUTPUT_PDX_TERRAIN Out;
            			Out.WorldSpacePos = Vertex.WorldSpacePos;

            			Out.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Vertex.WorldSpacePos, 1.0 ) );
            			Out.ShadowProj = mul( ShadowMapTextureMatrix, float4( Vertex.WorldSpacePos, 1.0 ) );

            			return Out;
            		}

            		// Copies of the pixels shader CalcHeightBlendFactors and CalcDetailUV functions
            		float4 CalcHeightBlendFactors( float4 MaterialHeights, float4 MaterialFactors, float BlendRange )
            		{
            			float4 Mat = MaterialHeights + MaterialFactors;
            			float BlendStart = max( max( Mat.x, Mat.y ), max( Mat.z, Mat.w ) ) - BlendRange;

            			float4 MatBlend = max( Mat - vec4( BlendStart ), vec4( 0.0 ) );

            			float Epsilon = 0.00001;
            			return float4( MatBlend ) / ( dot( MatBlend, vec4( 1.0 ) ) + Epsilon );
            		}

            		float2 CalcDetailUV( float2 WorldSpacePosXZ )
            		{
            			return (WorldSpacePosXZ + DetailTileOffset) * DetailTileFactor;
            		}

            		// A low spec vertex buffer version of CalculateDetails
            		void CalculateDetailsLowSpec( float2 WorldSpacePosXZ, out float3 DetailDiffuse, out float4 DetailMaterial )
            		{
            			float2 DetailCoordinates = WorldSpacePosXZ * WorldSpaceToDetail;
            			float2 DetailCoordinatesScaled = DetailCoordinates * DetailTextureSize;
            			float2 DetailCoordinatesScaledFloored = floor( DetailCoordinatesScaled );
            			float2 DetailCoordinatesFrac = DetailCoordinatesScaled - DetailCoordinatesScaledFloored;
            			DetailCoordinates = DetailCoordinatesScaledFloored * DetailTexelSize + DetailTexelSize * 0.5;

            			float4 Factors = float4(
            				(1.0 - DetailCoordinatesFrac.x) * (1.0 - DetailCoordinatesFrac.y),
            				DetailCoordinatesFrac.x * (1.0 - DetailCoordinatesFrac.y),
            				(1.0 - DetailCoordinatesFrac.x) * DetailCoordinatesFrac.y,
            				DetailCoordinatesFrac.x * DetailCoordinatesFrac.y
            			);

            			float4 DetailIndex = PdxTex2DLod0( DetailIndexTexture, DetailCoordinates ) * 255.0;
            			float4 DetailMask = PdxTex2DLod0( DetailMaskTexture, DetailCoordinates ) * Factors[0];

            			float2 Offsets[3];
            			Offsets[0] = float2( DetailTexelSize.x, 0.0 );
            			Offsets[1] = float2( 0.0, DetailTexelSize.y );
            			Offsets[2] = float2( DetailTexelSize.x, DetailTexelSize.y );

            			for ( int k = 0; k < 3; ++k )
            			{
            				float2 DetailCoordinates2 = DetailCoordinates + Offsets[k];

            				float4 DetailIndices = PdxTex2DLod0( DetailIndexTexture, DetailCoordinates2 ) * 255.0;
            				float4 DetailMasks = PdxTex2DLod0( DetailMaskTexture, DetailCoordinates2 ) * Factors[k+1];

            				for ( int i = 0; i < 4; ++i )
            				{
            					for ( int j = 0; j < 4; ++j )
            					{
            						if ( DetailIndex[j] == DetailIndices[i] )
            						{
            							DetailMask[j] += DetailMasks[i];
            						}
            					}
            				}
            			}

            			// We don't use different detail UVs per material like in the normal pdxterrain shader
            			float2 DetailUV = CalcDetailUV( WorldSpacePosXZ );

            			float4 DiffuseTexture0 = PdxTex2DLod0( DetailTextures, float3( DetailUV, DetailIndex[0] ) ) * smoothstep( 0.0, 0.1, DetailMask[0] );
            			float4 DiffuseTexture1 = PdxTex2DLod0( DetailTextures, float3( DetailUV, DetailIndex[1] ) ) * smoothstep( 0.0, 0.1, DetailMask[1] );
            			float4 DiffuseTexture2 = PdxTex2DLod0( DetailTextures, float3( DetailUV, DetailIndex[2] ) ) * smoothstep( 0.0, 0.1, DetailMask[2] );
            			float4 DiffuseTexture3 = PdxTex2DLod0( DetailTextures, float3( DetailUV, DetailIndex[3] ) ) * smoothstep( 0.0, 0.1, DetailMask[3] );

            			float4 BlendFactors = CalcHeightBlendFactors( float4( DiffuseTexture0.a, DiffuseTexture1.a, DiffuseTexture2.a, DiffuseTexture3.a ), DetailMask, DetailBlendRange );

            			DetailDiffuse = DiffuseTexture0.rgb * BlendFactors[0] +
            							DiffuseTexture1.rgb * BlendFactors[1] +
            							DiffuseTexture2.rgb * BlendFactors[2] +
            							DiffuseTexture3.rgb * BlendFactors[3];

            			DetailMaterial = vec4( 0.0 );

            			for ( int i = 0; i < 4; ++i )
            			{
            				float BlendFactor = BlendFactors[i];
            				if ( BlendFactor > 0.0 )
            				{
            					float3 ArrayUV = float3( DetailUV, DetailIndex[i] );
            					float4 NormalTexture = PdxTex2DLod0( NormalTextures, ArrayUV );
            					float4 MaterialTexture = PdxTex2DLod0( MaterialTextures, ArrayUV );

            					DetailMaterial += MaterialTexture * BlendFactor;
            				}
            			}
            		}

            		VS_OUTPUT_PDX_TERRAIN_LOW_SPEC TerrainVertexLowSpec( float2 WithinNodePos, float2 NodeOffset, float NodeScale, float2 LodDirection, float LodLerpFactor )
            		{
            			STerrainVertex Vertex = CalcTerrainVertex( WithinNodePos, NodeOffset, NodeScale, LodDirection, LodLerpFactor );

            			#ifdef TERRAIN_FLAT_MAP_LERP
            				Vertex.WorldSpacePos.y = lerp( Vertex.WorldSpacePos.y, FlatMapHeight + 10, FlatMapLerp );
            			#endif
            			#ifdef TERRAIN_FLAT_MAP
            				Vertex.WorldSpacePos.y = FlatMapHeight + 10;
            			#endif

            			VS_OUTPUT_PDX_TERRAIN_LOW_SPEC Out;
            			Out.WorldSpacePos = Vertex.WorldSpacePos;

            			Out.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Vertex.WorldSpacePos, 1.0 ) );
            			Out.ShadowProj = mul( ShadowMapTextureMatrix, float4( Vertex.WorldSpacePos, 1.0 ) );

            			CalculateDetailsLowSpec( Vertex.WorldSpacePos.xz, Out.DetailDiffuse, Out.DetailMaterial );

            			float2 ColorMapCoords = Vertex.WorldSpacePos.xz * WorldSpaceToTerrain0To1;

            #if defined( PDX_OSX ) && defined( PDX_OPENGL )
            			// We're limited to the amount of samplers we can bind at any given time on Mac, so instead
            			// we disable the usage of ColorTexture (since its effects are very subtle) and assign a
            			// default value here instead.
            			Out.ColorMap = float3( vec3( 0.5 ) );
            #else
            			Out.ColorMap = ToLinear( PdxTex2DLod0( ColorTexture, float2( ColorMapCoords.x, 1.0 - ColorMapCoords.y ) ).rgb );
            #endif

            			Out.FlatMap = float3( vec3( 0.5f ) ); // neutral overlay
            			#ifdef TERRAIN_FLAT_MAP_LERP
            				Out.FlatMap = lerp( Out.FlatMap, PdxTex2DLod0( FlatMapTexture, float2( ColorMapCoords.x, 1.0 - ColorMapCoords.y ) ).rgb, FlatMapLerp );
            			#endif

            			Out.Normal = CalculateNormal( Vertex.WorldSpacePos.xz );

            			return Out;
            		}
            	]]

            	MainCode VertexShader
            	{
            		Input = "VS_INPUT_PDX_TERRAIN"
            		Output = "VS_OUTPUT_PDX_TERRAIN"
            		Code
            		[[
            			PDX_MAIN
            			{
            				return TerrainVertex( Input.UV, Input.NodeOffset_Scale_Lerp.xy, Input.NodeOffset_Scale_Lerp.z, Input.LodDirection, Input.NodeOffset_Scale_Lerp.w );
            			}
            		]]
            	}

            	MainCode VertexShaderSkirt
            	{
            		Input = "VS_INPUT_PDX_TERRAIN_SKIRT"
            		Output = "VS_OUTPUT_PDX_TERRAIN"
            		Code
            		[[
            			PDX_MAIN
            			{
            				VS_OUTPUT_PDX_TERRAIN Out = TerrainVertex( Input.UV, Input.NodeOffset_Scale_Lerp.xy, Input.NodeOffset_Scale_Lerp.z, Input.LodDirection, Input.NodeOffset_Scale_Lerp.w );

            				float3 Position = FixPositionForSkirt( Out.WorldSpacePos, Input.VertexID );
            				Out.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Position, 1.0 ) );

            				return Out;
            			}
            		]]
            	}

            	MainCode VertexShaderLowSpec
            	{
            		Input = "VS_INPUT_PDX_TERRAIN"
            		Output = "VS_OUTPUT_PDX_TERRAIN_LOW_SPEC"
            		Code
            		[[
            			PDX_MAIN
            			{
            				return TerrainVertexLowSpec( Input.UV, Input.NodeOffset_Scale_Lerp.xy, Input.NodeOffset_Scale_Lerp.z, Input.LodDirection, Input.NodeOffset_Scale_Lerp.w );
            			}
            		]]
            	}

            	MainCode VertexShaderLowSpecSkirt
            	{
            		Input = "VS_INPUT_PDX_TERRAIN_SKIRT"
            		Output = "VS_OUTPUT_PDX_TERRAIN_LOW_SPEC"
            		Code
            		[[
            			PDX_MAIN
            			{
            				VS_OUTPUT_PDX_TERRAIN_LOW_SPEC Out = TerrainVertexLowSpec( Input.UV, Input.NodeOffset_Scale_Lerp.xy, Input.NodeOffset_Scale_Lerp.z, Input.LodDirection, Input.NodeOffset_Scale_Lerp.w );

            				float3 Position = FixPositionForSkirt( Out.WorldSpacePos, Input.VertexID );
            				Out.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Position, 1.0 ) );

            				return Out;
            			}
            		]]
            	}
            }


            PixelShader =
            {
            	# PdxTerrain uses texture index 0 - 6

            	# Jomini specific
            	TextureSampler ShadowMap
            	{
            		Ref = PdxShadowmap
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Wrap"
            		SampleModeV = "Wrap"
            		CompareFunction = less_equal
            		SamplerType = "Compare"
            	}

            	# Game specific
            	TextureSampler FogOfWarAlpha
            	{
            		Ref = JominiFogOfWar
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Wrap"
            		SampleModeV = "Wrap"
            	}
            	TextureSampler FlatMapTexture
            	{
            		Ref = TerrainFlatMap
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Clamp"
            		SampleModeV = "Clamp"
            	}
            	TextureSampler EnvironmentMap
            	{
            		Ref = JominiEnvironmentMap
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Clamp"
            		SampleModeV = "Clamp"
            		Type = "Cube"
            	}
            	TextureSampler FlatMapEnvironmentMap
            	{
            		Ref = FlatMapEnvironmentMap
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Clamp"
            		SampleModeV = "Clamp"
            		Type = "Cube"
            	}
            	TextureSampler SurroundFlatMapMask
            	{
            		Ref = SurroundFlatMapMask
            		MagFilter = "Linear"
            		MinFilter = "Linear"
            		MipFilter = "Linear"
            		SampleModeU = "Border"
            		SampleModeV = "Border"
            		Border_Color = { 1 1 1 1 }
            		File = "gfx/map/surround_map/surround_mask.dds"
            	}

            	Code
            	[[
            		static const float UNDERWATER_CLIP_OFFSET = 0.00001f;
            		static const float TERRAIN_SKIRT_CLIP_OFFSET = 0.01f;
            		SLightingProperties GetFlatMapLerpSunLightingProperties( float3 WorldSpacePos, float ShadowTerm )
            		{
            			SLightingProperties LightingProps;
            			LightingProps._ToCameraDir = normalize( CameraPosition - WorldSpacePos );
            			LightingProps._ToLightDir = ToSunDir;
            			LightingProps._LightIntensity = FlatMapLerpSunDiffuse * 5;
            			LightingProps._ShadowTerm = ShadowTerm;
            			LightingProps._CubemapIntensity = FlatMapLerpCubemapIntensity;
            			LightingProps._CubemapYRotation = FlatMapLerpCubemapYRotation;

            			return LightingProps;
            		}
            		void CheckClipNeeded( float TerrainHeight, float2 MapCoords, float StartColorOverlayHeightBlend )
            		{
            			#ifdef TERRAIN_SKIRT
            				clip( TerrainHeight - TERRAIN_SKIRT_CLIP_OFFSET );
            			#endif

            			#ifdef UNDERWATER
            				// When doing the refraction pass and applying the Color Overlay, skip the parts above the ocean.
            				if ( StartColorOverlayHeightBlend > 0.99f )
            				{
            					clip( _RefractionCullHeight - TerrainHeight );
            				}
            			#endif
            			clip( vec2( 1.0f ) - MapCoords );
            		}

            	]]

            	MainCode PixelShader
            	{
            		Input = "VS_OUTPUT_PDX_TERRAIN"
            		Output = "PDX_COLOR"
            		Code
            		[[

            			PDX_MAIN
            			{
            				float FullColorOverlayFactor = 0.0f;
            				bool IsFullyColorOverlay = false;

            				const float2 ColorMapCoords = Input.WorldSpacePos.xz * WorldSpaceToTerrain0To1;
            				CheckClipNeeded( Input.WorldSpacePos.y, ColorMapCoords, _StartColorOverlayHeightBlend * _EnabledTerrainCulling );

            			#ifndef UNDERWATER
            				// Skip terrain rendering below the ocean surface.
            				if ( Input.WorldSpacePos.y < UNDERWATER_CLIP_OFFSET && _EnabledTerrainCulling > 0.99f)
            				{
            					return float4( _UnderwaterTerrainColor.rgb, 0.0f );
            				}
            			#endif

            				float3 FlatMap = float3( 0.5f, 0.5f, 0.5f ); // neutral overlay
            				#ifdef TERRAIN_FLAT_MAP_LERP
            					FlatMap = lerp( FlatMap, PdxTex2D( FlatMapTexture,
            						float2( ColorMapCoords.x, 1.0f - ColorMapCoords.y ) ).rgb,
            						FlatMapLerp );
            				#endif

            				#ifdef TERRAIN_COLOR_OVERLAY
            					float3 BorderColor;
            					float BorderPreLightingBlend;
            					float BorderPostLightingBlend;
            					GetBorderColorAndBlendGame( Input.WorldSpacePos.xz, FlatMap, BorderColor, BorderPreLightingBlend, BorderPostLightingBlend );

            					FullColorOverlayFactor = BorderPreLightingBlend + BorderPostLightingBlend;
            					FullColorOverlayFactor *= _FullyColorOverlayHeightBlend * _EnabledTerrainCulling;
            				#endif
            				if ( FullColorOverlayFactor > 0.99f )
            				{
            					IsFullyColorOverlay = true;
            				}

            				float4 DetailDiffuse = vec4( 0.0f );
            				float3 DetailNormal = float3( 0.0f, 1.0f, 0.0f );
            				float4 DetailMaterial = vec4( 0.0f );
            				float ShadowTerm = 1.0f;
            				if( !IsFullyColorOverlay )
            				{
            					CalculateDetails( Input.WorldSpacePos.xz, DetailDiffuse, DetailNormal, DetailMaterial );
            					ShadowTerm = CalculateShadow( Input.ShadowProj, ShadowMap );
            				}

            				float FogOfWarAlphaValue = PdxTex2D( FogOfWarAlpha, ColorMapCoords).r;
            #if defined( PDX_OSX ) && defined( PDX_OPENGL )
            				// We're limited to the amount of samplers we can bind at any given time on Mac, so instead
            				// we disable the usage of ColorTexture (since its effects are very subtle) and assign a
            				// default value here instead.
            				float3 ColorMap = float3( vec3( 0.5f ) );
            				float ColorDarken = 1.0f;
            #else
            				float4 ColorMapSample = ToLinear( PdxTex2D( ColorTexture,
            						float2( ColorMapCoords.x, 1.0f - ColorMapCoords.y ) ) );
            				float ColorDarken = ColorMapSample.a;
            				float3 ColorMap = ColorMapSample.rgb;
            #endif

            				float SnowHighlight = 0.0f;
            				float3 Normal = CalculateNormal( Input.WorldSpacePos.xz );
            				#ifndef UNDERWATER
            					float3 ReorientedNormal = Normal;
            					if( !IsFullyColorOverlay )
            					{
            						float WaterNormalLerp = 0.0f;
            						EffectIntensities ConditionData;
            						BilinearSampleProvinceEffectsMask( ColorMapCoords, ConditionData );
            						ApplyProvinceEffectsTerrain( ConditionData, DetailDiffuse, DetailNormal, DetailMaterial, Input.WorldSpacePos, WaterNormalLerp );

            						// Use the property that only water has lower roughness to adjust the terrain normals to face upward.
            						float WaterNormalAdjustment = smoothstep( 0.6f, 1.0f, 1 - DetailMaterial.a);
            						WaterNormalLerp = max( WaterNormalLerp, WaterNormalAdjustment);
            						float3 ReorientedNormal = ReorientNormal(
            							lerp( Normal, float3( 0.0f, 1.0f, 0.0f ), WaterNormalLerp ),
            							DetailNormal );

            						ApplySnowMaterialTerrain( DetailDiffuse, DetailNormal, DetailMaterial, Normal, Input.WorldSpacePos.xz, ColorMapCoords, SnowHighlight );

            						if( ConditionData._Drought > 0.0f || SnowHighlight > 0.0f )
            						{
            							ShadowTerm = lerp( ShadowTerm + 0.4f , ShadowTerm , ShadowTerm );
            						}
            					}
            				#else
            					float3 ReorientedNormal = ReorientNormal( Normal, DetailNormal );
            				#endif

            				float3 Diffuse = SoftLight( DetailDiffuse.rgb, ColorMap,
            					( 1 - DetailMaterial.r ) * COLORMAP_OVERLAY_STRENGTH );

            				#ifdef TERRAIN_COLOR_OVERLAY
            					LerpBorderColorWithFogOfWarAlphaValue( Diffuse, FogOfWarAlphaValue, BorderColor, BorderPreLightingBlend );
            					#ifdef TERRAIN_FLAT_MAP_LERP
            						float3 FlatColor;
            						GetBorderColorAndBlendGameLerp( Input.WorldSpacePos.xz, FlatMap,
            							FlatColor, BorderPreLightingBlend, BorderPostLightingBlend,
            							FlatMapLerp );

            						FlatMap = lerp( FlatMap, FlatColor,
            							saturate( BorderPreLightingBlend + BorderPostLightingBlend ) );
            					#endif
            					float4 HighlightColor = GetHighlightColor( ColorMapCoords );
            					ApplyHighlightColor( Diffuse, HighlightColor );
            					CompensateWhiteHighlightColor( Diffuse, HighlightColor, SnowHighlight );
            				#endif

            				SMaterialProperties MaterialProps = GetMaterialProperties(
            					Diffuse,
            					ReorientedNormal,
            					DetailMaterial.a,
            					DetailMaterial.g,
            					DetailMaterial.b
            				);

            				SLightingProperties LightingProps = GetMapLightingProperties( Input.WorldSpacePos, ShadowTerm );
            				#ifdef TERRAIN_FLAT_MAP_LERP
            					LightingProps._LightIntensity = lerp( TERRAIN_SUNNY_SUN_COLOR * TERRAIN_SUNNY_SUN_INTENSITY, FlatMapLerpSunIntensity * SunDiffuse, FlatMapLerp );
            					LightingProps._CubemapIntensity =  lerp( DefaultEnvironmentCubemapIntensity * TERRAIN_SUNNY_IBL_SCALE, FlatMapLerpCubemapIntensity , FlatMapLerp );
            					LightingProps._ToLightDir = lerp( ToTerrainSunnySunDir, ToSunDir , FlatMapLerp );
            				#endif

            				// Calculate combined shadow mask from clouds and shadow tint
            				float CloudMask = 0.0f;
            				float3 FinalColor = vec3( 0.0f );
            				if( !IsFullyColorOverlay )
            				{
            					CloudMask = GetCloudShadowMask( Input.WorldSpacePos.xz, FogOfWarAlphaValue );
            					FinalColor = CalculateTerrainDualScenarioLighting( LightingProps, MaterialProps, CloudMask, EnvironmentMap );
            					// Apply shadow tint with cloud interaction for terrain
            					FinalColor = ApplyTerrainShadowTintWithClouds( FinalColor, Input.WorldSpacePos.xz, CloudMask, ShadowTerm, ReorientedNormal, Normal );
            					float BlendAmount = ( 1.0f - ColorDarken ) * CloudMask; // Combine color mask with cloud coverage
            					FinalColor.rgb = ApplyOvercastContrast( FinalColor, BlendAmount );
            				}

            				#ifdef TERRAIN_COLOR_OVERLAY
            				 	float NdotL = saturate( dot( MaterialProps._Normal, LightingProps._ToLightDir ) ) + 1e-5;
            					BorderColor *= lerp( max( _WaterZoomedInZoomedOutFactor - 0.4f, 0.4f ), 1.0f, NdotL );
            					FinalColor.rgb = lerp( FinalColor.rgb, BorderColor, BorderPostLightingBlend );
            					ApplyHighlightColor( FinalColor.rgb, HighlightColor, 0.25f );
            					ApplyDiseaseDiffuse( FinalColor, ColorMapCoords );
            					ApplyLegendDiffuse( FinalColor, ColorMapCoords );
            				#endif

            				#ifndef UNDERWATER
            					if( !IsFullyColorOverlay )
            					{
            						FinalColor = ApplyFogOfWar( FinalColor, Input.WorldSpacePos, FogOfWarAlpha );
            						FinalColor = ApplyMapDistanceFogWithoutFoW( FinalColor, Input.WorldSpacePos );
            					}	
            				#endif

            				#ifdef TERRAIN_FLAT_MAP_LERP
            					float Blend = CalculatePaperTransitionBlend( ColorMapCoords, FlatMapLerp );
            					FlatMap = ApplyFlatMapBrightnessAdjustment( FlatMap );
            					FinalColor = lerp( FinalColor, FlatMap, Blend );
            				#endif

            				float Alpha = 1.0f;
            				#ifdef UNDERWATER
            					Alpha = CompressWorldSpace( Input.WorldSpacePos );
            				#endif

            				#ifdef TERRAIN_DEBUG
            					TerrainDebug( FinalColor, Input.WorldSpacePos );
            				#endif
            				// DebugReturn( FinalColor, MaterialProps, LightingProps, EnvironmentMap );

            				return float4( FinalColor, Alpha );
            			}
            		]]
            	}

            	MainCode PixelShaderLowSpec
            	{
            		Input = "VS_OUTPUT_PDX_TERRAIN_LOW_SPEC"
            		Output = "PDX_COLOR"
            		Code
            		[[
            			PDX_MAIN
            			{
            				float FullColorOverlayFactor = 0.0f;
            				bool IsFullyColorOverlay = false;

            				const float2 ColorMapCoords = Input.WorldSpacePos.xz * WorldSpaceToTerrain0To1;
            				CheckClipNeeded( Input.WorldSpacePos.y, ColorMapCoords, _StartColorOverlayHeightBlend);

            				float3 DetailDiffuse = Input.DetailDiffuse;
            				float4 DetailMaterial = Input.DetailMaterial;
            				float3 ColorMap = Input.ColorMap;
            				float3 FlatMap = Input.FlatMap;
            				float3 Normal = Input.Normal;

            				#ifdef TERRAIN_COLOR_OVERLAY
            					float3 BorderColor;
            					float BorderPreLightingBlend;
            					float BorderPostLightingBlend;
            					GetBorderColorAndBlendGame( Input.WorldSpacePos.xz, FlatMap, BorderColor, BorderPreLightingBlend, BorderPostLightingBlend );

            					FullColorOverlayFactor = BorderPreLightingBlend + BorderPostLightingBlend;
            					FullColorOverlayFactor *= _FullyColorOverlayHeightBlend * _EnabledTerrainCulling;
            				#endif
            				if ( FullColorOverlayFactor > 0.99f )
            				{
            					IsFullyColorOverlay = true;
            				}

            				float SnowHighlight = 0.0f;
            				#ifndef UNDERWATER
            					DetailDiffuse = ApplyDynamicMasksDiffuse( DetailDiffuse, Normal, ColorMapCoords );
            				#endif

            				float3 Diffuse = SoftLight( DetailDiffuse.rgb, ColorMap, ( 1 - DetailMaterial.r ) * COLORMAP_OVERLAY_STRENGTH );

            				#ifdef TERRAIN_COLOR_OVERLAY
            					float FogOfWarAlphaValue = PdxTex2D( FogOfWarAlpha, ColorMapCoords).r;
            					LerpBorderColorWithFogOfWarAlphaValue( Diffuse, FogOfWarAlphaValue, BorderColor, BorderPreLightingBlend );

            					#ifdef TERRAIN_FLAT_MAP_LERP
            						float3 FlatColor;
            						GetBorderColorAndBlendGameLerp( Input.WorldSpacePos.xz, FlatMap,
            							FlatColor, BorderPreLightingBlend, BorderPostLightingBlend,
            							FlatMapLerp );
            						FlatMap = lerp( FlatMap, FlatColor,
            							saturate( BorderPreLightingBlend + BorderPostLightingBlend ) );
            					#endif
            				#endif

            				float3 FinalColor = vec3( 0.0f );
            				SMaterialProperties MaterialProps = GetMaterialProperties(
            					Diffuse,
            					Normal,
            					DetailMaterial.a,
            					DetailMaterial.g,
            					DetailMaterial.b
            				);
            				float ShadowTerm = 1.0f;
            				SLightingProperties LightingProps = GetMapLightingProperties( Input.WorldSpacePos, ShadowTerm );
            				if( !IsFullyColorOverlay )
            				{
            					FinalColor = CalculateTerrainSunLightingLowSpec( MaterialProps, LightingProps );
            				}
            				#ifndef UNDERWATER
            					if( !IsFullyColorOverlay )
            					{
            						FinalColor = ApplyFogOfWar( FinalColor, Input.WorldSpacePos, FogOfWarAlpha );
            						FinalColor = ApplyMapDistanceFog( FinalColor, Input.WorldSpacePos, FogOfWarAlpha );
            					}
            				#endif

            				#ifdef TERRAIN_COLOR_OVERLAY
            					FinalColor.rgb = lerp( FinalColor.rgb, BorderColor, BorderPostLightingBlend );
            				#endif

            				#ifdef TERRAIN_COLOR_OVERLAY
            					float4 HighlightColor = GetHighlightColor( ColorMapCoords );
            					ApplyHighlightColor( FinalColor.rgb, HighlightColor );
            					CompensateWhiteHighlightColor( FinalColor.rgb, HighlightColor, SnowHighlight );
            				#endif

            				#ifdef TERRAIN_FLAT_MAP_LERP
            					FinalColor = lerp( FinalColor, FlatMap, FlatMapLerp );
            				#endif

            				float Alpha = 1.0f;
            				#ifdef UNDERWATER
            					Alpha = CompressWorldSpace( Input.WorldSpacePos );
            				#endif

            				#ifdef TERRAIN_DEBUG
            					TerrainDebug( FinalColor, Input.WorldSpacePos );
            				#endif

            				DebugReturn( FinalColor, MaterialProps, LightingProps, EnvironmentMap );
            				return float4( FinalColor, Alpha );
            			}
            		]]
            	}

            	MainCode PixelShaderFlatMap
            	{
            		Input = "VS_OUTPUT_PDX_TERRAIN"
            		Output = "PDX_COLOR"
            		Code
            		[[
            			PDX_MAIN
            			{
            				#ifdef TERRAIN_SKIRT
            					return float4( 0, 0, 0, 0 );
            				#endif

            				clip( vec2( 1.0f ) - Input.WorldSpacePos.xz * WorldSpaceToTerrain0To1 );

            				float2 ColorMapCoords = Input.WorldSpacePos.xz * WorldSpaceToTerrain0To1;
            				float3 FlatMap = PdxTex2D( FlatMapTexture, float2( ColorMapCoords.x, 1.0 - ColorMapCoords.y ) ).rgb;


            				#ifdef TERRAIN_COLOR_OVERLAY
            					float3 BorderColor;
            					float BorderPreLightingBlend;
            					float BorderPostLightingBlend;

            					GetBorderColorAndBlendGameLerp( Input.WorldSpacePos.xz, FlatMap,
            						BorderColor, BorderPreLightingBlend, BorderPostLightingBlend,
            						1.0f );

            					FlatMap = lerp( FlatMap, BorderColor,
            						saturate( BorderPreLightingBlend + BorderPostLightingBlend ) );

            				#endif

            				float3 FinalColor = FlatMap;
            				#ifdef TERRAIN_COLOR_OVERLAY
            					float4 HighlightColor = GetHighlightColor( ColorMapCoords );
            					ApplyHighlightColor( FinalColor, HighlightColor, 0.5f );
            				#endif

            				#ifdef TERRAIN_DEBUG
            					TerrainDebug( FinalColor, Input.WorldSpacePos );
            				#endif

            				FinalColor = ApplyFlatMapBrightnessAdjustment( FinalColor );

            				// Make flatmap transparent based on the SurroundFlatMapMask
            				float SurroundMapAlpha = 1 - PdxTex2D( SurroundFlatMapMask, float2( ColorMapCoords.x, 1.0 - ColorMapCoords.y ) ).b;
            				SurroundMapAlpha *= FlatMapLerp;

            				return float4( FinalColor, SurroundMapAlpha );
            			}
            		]]
            	}
            }


            Effect PdxTerrain
            {
            	VertexShader = "VertexShader"
            	PixelShader = "PixelShader"

            	Defines = { "TERRAIN_FLAT_MAP_LERP" }
            }

            Effect PdxTerrainLowSpec
            {
            	VertexShader = "VertexShaderLowSpec"
            	PixelShader = "PixelShaderLowSpec"
            }

            Effect PdxTerrainSkirt
            {
            	VertexShader = "VertexShaderSkirt"
            	PixelShader = "PixelShader"
            	Defines = { "TERRAIN_SKIRT" }
            }

            Effect PdxTerrainLowSpecSkirt
            {
            	VertexShader = "VertexShaderLowSpecSkirt"
            	PixelShader = "PixelShaderLowSpec"
            	Defines = { "TERRAIN_SKIRT" }
            }

            ### FlatMap Effects

            BlendState BlendStateAlpha
            {
            	BlendEnable = yes
            	SourceBlend = "SRC_ALPHA"
            	DestBlend = "INV_SRC_ALPHA"
            }

            Effect PdxTerrainFlat
            {
            	VertexShader = "VertexShader"
            	PixelShader = "PixelShaderFlatMap"
            	BlendState = BlendStateAlpha

            	Defines = { "TERRAIN_FLAT_MAP" "TERRAIN_FLATMAP_LIGHTING" }
            }

            Effect PdxTerrainFlatSkirt
            {
            	VertexShader = "VertexShaderSkirt"
            	PixelShader = "PixelShaderFlatMap"
            	BlendState = BlendStateAlpha

            	Defines = { "TERRAIN_FLAT_MAP" "TERRAIN_SKIRT" }
            }

            # Low Spec flat map the same as regular effect
            Effect PdxTerrainFlatLowSpec
            {
            	VertexShader = "VertexShader"
            	PixelShader = "PixelShaderFlatMap"
            	BlendState = BlendStateAlpha

            	Defines = { "TERRAIN_FLAT_MAP" }
            }

            Effect PdxTerrainFlatLowSpecSkirt
            {
            	VertexShader = "VertexShaderSkirt"
            	PixelShader = "PixelShaderFlatMap"
            	BlendState = BlendStateAlpha

            	Defines = { "TERRAIN_FLAT_MAP" "TERRAIN_SKIRT" }
            }
            """;
        var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "Fx", "pdxterrain.shader");
        Helper.EnsureDirectoryExists(path);
        await File.WriteAllTextAsync(path, file);
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
    public static async Task WriteGraphics()
    {
        var file = $@"
NCamera = {{
	FOV	= 60 # Field-of-View
	ZNEAR = 10
	ZFAR = 10000

	EDGE_SCROLLING_PIXELS = 10 # how many pixels from window edge that will trigger edge scrolling

	SCROLL_SPEED = 0.045 # higher values = faster camera. NOTE that this is tweakables from settings as well!
	ZOOM_RATE = 0.2 # Zoom when right-mouse down
	ZOOM_STEPS = {{ 100 125 146 165 183 204 229 260 300 350 405 461 518 578 643 714 793 881 981 1092 1218 1360 1521 1703 1903 2116 2341 2573 2809 3047 3282 3512 3733 }}	# Zoom steps
	# STEPS					0  1  2  3  4  5  6  7  8  9  10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25 26 27 28 29 30 31 32
	ZOOM_STEPS_TILT = {{	 50 53 56 59 62 65 67 70 72 74 76 77 79 80 82 83 83 84 85 85 85 85 85 85 85 85 85 85 85 85 85 85 85 }}		# Defualt zoom tilt
	#ZOOM_STEPS_TILT = {{	45 45 45 45 45 45 45 45 45 45 45 45 45 45 45 47 49 51 53 55 55 55 55 55 55 55 55 55 55 55 55 55 55 }}		# Tweak Zoom steps
	ZOOM_STEPS_MIN_TILT = {{ 40 41 43 44 45 46 47 48 49 50 51 52 52 53 54 54 54 55 55 55 55 55 55 55 55 55 55 55 55 55 55 55 55 }}
	ZOOM_STEPS_MAX_TILT = {{ 70 73 76 78 80 82 84 85 86 87 88 88 89 89 89 89 89 89 89 89 89 89 89 89 89 89 89 89 89 89 89 89 89 }}
	ZOOM_AUDIO_PARAMETER_SCALE = 0.1		# The audio parameter ""CameraHeight"" will be set to the camera's height X ZOOM_AUDIO_PARAMETER_SCALE




	MAX_PAN_TO_ZOOM_STEP = 4							# The camera will lower to this height (but only if above it) when panning to characters/provinces etc.
	START_LOOK_AT = {{ 4825.0 0 1900.0 }} # {{ 2600.0 0 2250.0 }}					# Initial look at (X/Y)

	# Debug defines
	DEBUG_GAMEPAD_LOWSPEED 		= 25.0
	DEBUG_GAMEPAD_NORMALSPEED 	= 100.0
	DEBUG_GAMEPAD_HIGHSPEED 	= 300.0
	DEBUG_GAMEPAD_SENSITIVITY 	= 2.0

	TITLE_ZOOM_LEVEL_BY_EXTENT = {{ 20 15 13 11 9 7 5 4 3 }}
	TITLE_ZOOM_LEVEL_EXTENTS = {{ 1000 800 600 400 300 200 100 -1 }}
	TITLE_ZOOM_OFFSET_IF_LEFT_VIEW_SHOWN = {{ 230 175 145 120 95 70 50 40 30 }} # We pretend the center point of the title is this far to the west if a left-view (E.G., the title view) is shown, and no right-view. It ensures that it ends up centered in the part of the screen not obscured by the UI

	PANNING_WIDTH =  {Settings.Instance.MapWidth}
	PANNING_HEIGHT = {Settings.Instance.MapHeight}
}}
";
        var path = Helper.GetPath(Settings.OutputDirectory, "common", "defines", "graphic", "00_graphics.txt");
        Helper.EnsureDirectoryExists(path);
        File.WriteAllText(path, file, new UTF8Encoding(true));
    }
    public static async Task WriteDefines(Map map)
    {
        var maxElevation = Settings.Instance.MaxElevation;
        var waterLevel = ((float)Settings.Instance.MaxElevation / 255) * HeightMapConverter.CK3WaterLevel;
        var file = $@"NJominiMap = {{
	WORLD_EXTENTS_X = {map.Settings.MapWidth - 1}
	WORLD_EXTENTS_Y = {maxElevation}
	WORLD_EXTENTS_Z = {map.Settings.MapHeight - 1}
	WATERLEVEL = {waterLevel}
}}";
        var path = Helper.GetPath(Settings.OutputDirectory, "common", "defines", "00_defines.txt");
        Helper.EnsureDirectoryExists(path);
        await File.WriteAllTextAsync(path, file, new UTF8Encoding(true));
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
            .ToDictionary(n => n.id, n => n.province.Cells.GetDominant(m => m.culture));

        //string[] baronyNames = ["Montura", "Rociagamia", "Pianti", "Menturno", "Monziodolon", "Rocciamigna", "Arbogiomia", "Selcivero", "Pogna", "Brocasia", "Arnole"];
        //var baronies = map.Output.Empires
        //   .SelectMany(n => n.kingdoms)
        //   .SelectMany(n => n.duchies)
        //   .SelectMany(n => n.counties)
        //   .SelectMany(n => n.baronies);
        //var islandBaronies = baronies.Where(n => baronyNames.Contains(n.name, StringComparer.OrdinalIgnoreCase)).ToArray();
        //var islandBaroniesCultures = islandBaronies.ToDictionary(n => n.id, n => n.province.Cells.GetDominant(m => m.culture));

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
        //        toOriginalCultureName[i] = originalCultureNames[i];
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
            File.WriteAllText(Helper.GetPath(provincesPath , Path.GetFileName(p)), "");
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
