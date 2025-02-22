using ImageMagick;
using Microsoft.VisualBasic.FileIO;
using SixLabors.ImageSharp;
using Svg;
using System.Diagnostics;
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
                Width = (int)map.Settings.MapWidth,
                Height = (int)map.Settings.MapHeight,
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
                Width = (int)map.Settings.MapWidth,
                Height = (int)map.Settings.MapHeight,
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

            var svg = SvgDocument.FromSvg<SvgDocument>(terrain.OuterXml);
            var img = svg.ToImage(map.Settings.MapWidth, map.Settings.MapHeight);
            var base64 = img.ToBase64String(SixLabors.ImageSharp.Formats.Png.PngFormat.Instance);
            var magickImage = MagickImage.FromBase64(base64.Split(',')[1]);
            //magickImage.Write("terrain.dds", MagickFormat.Dds);

            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "flatmap.dds");
            Helper.EnsureDirectoryExists(path);
            magickImage.Write(path, MagickFormat.Dds);
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
        var file = $"Includes = {{\r\n\t\"cw/pdxterrain.fxh\"\r\n\t\"cw/heightmap.fxh\"\r\n\t\"cw/shadow.fxh\"\r\n\t\"cw/utility.fxh\"\r\n\t\"cw/camera.fxh\"\r\n\t\"jomini/jomini_fog.fxh\"\r\n\t\"jomini/jomini_lighting.fxh\"\r\n\t\"jomini/jomini_fog_of_war.fxh\"\r\n\t\"jomini/jomini_water.fxh\"\r\n\t\"standardfuncsgfx.fxh\"\r\n\t\"bordercolor.fxh\"\r\n\t\"lowspec.fxh\"\r\n\t\"legend.fxh\"\r\n\t\"cw/lighting.fxh\"\r\n\t\"dynamic_masks.fxh\"\r\n\t\"disease.fxh\"\r\n}}\r\n\r\nVertexStruct VS_OUTPUT_PDX_TERRAIN\r\n{{\r\n\tfloat4 Position\t\t\t: PDX_POSITION;\r\n\tfloat3 WorldSpacePos\t: TEXCOORD1;\r\n\tfloat4 ShadowProj\t\t: TEXCOORD2;\r\n}};\r\n\r\nVertexStruct VS_OUTPUT_PDX_TERRAIN_LOW_SPEC\r\n{{\r\n\tfloat4 Position\t\t\t: PDX_POSITION;\r\n\tfloat3 WorldSpacePos\t: TEXCOORD1;\r\n\tfloat4 ShadowProj\t\t: TEXCOORD2;\r\n\tfloat3 DetailDiffuse\t: TEXCOORD3;\r\n\tfloat4 DetailMaterial\t: TEXCOORD4;\r\n\tfloat3 ColorMap\t\t\t: TEXCOORD5;\t\t\r\n\tfloat3 FlatMap\t\t\t: TEXCOORD6;\r\n\tfloat3 Normal\t\t\t: TEXCOORD7;\r\n}};\r\n\r\n# Limited JominiEnvironment data to get nicer transitions between the Flatmap lighting and Terrain lighting\r\n# Only used in terrain shader while lerping between flatmap and terrain.\r\nConstantBuffer( FlatMapLerpEnvironment )\r\n{{\r\n\tfloat\tFlatMapLerpCubemapIntensity;\r\n\tfloat3\tFlatMapLerpSunDiffuse;\r\n\tfloat\tFlatMapLerpSunIntensity;\r\n\tfloat4x4 FlatMapLerpCubemapYRotation;\r\n}};\r\n\r\nVertexShader =\r\n{{\r\n\tTextureSampler DetailTextures\r\n\t{{\r\n\t\tRef = PdxTerrainTextures0\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Wrap\"\r\n\t\tSampleModeV = \"Wrap\"\r\n\t\ttype = \"2darray\"\r\n\t}}\r\n\tTextureSampler NormalTextures\r\n\t{{\r\n\t\tRef = PdxTerrainTextures1\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Wrap\"\r\n\t\tSampleModeV = \"Wrap\"\r\n\t\ttype = \"2darray\"\r\n\t}}\r\n\tTextureSampler MaterialTextures\r\n\t{{\r\n\t\tRef = PdxTerrainTextures2\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Wrap\"\r\n\t\tSampleModeV = \"Wrap\"\r\n\t\ttype = \"2darray\"\r\n\t}}\r\n\tTextureSampler DetailIndexTexture\r\n\t{{\r\n\t\tRef = PdxTerrainTextures3\r\n\t\tMagFilter = \"Point\"\r\n\t\tMinFilter = \"Point\"\r\n\t\tMipFilter = \"Point\"\r\n\t\tSampleModeU = \"Clamp\"\r\n\t\tSampleModeV = \"Clamp\"\r\n\t}}\r\n\tTextureSampler DetailMaskTexture\r\n\t{{\r\n\t\tRef = PdxTerrainTextures4\r\n\t\tMagFilter = \"Point\"\r\n\t\tMinFilter = \"Point\"\r\n\t\tMipFilter = \"Point\"\r\n\t\tSampleModeU = \"Clamp\"\r\n\t\tSampleModeV = \"Clamp\"\r\n\t}}\r\n\tTextureSampler ColorTexture\r\n\t{{\r\n\t\tRef = PdxTerrainColorMap\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Clamp\"\r\n\t\tSampleModeV = \"Clamp\"\r\n\t}}\r\n\tTextureSampler FlatMapTexture\r\n\t{{\r\n\t\tRef = TerrainFlatMap\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Clamp\"\r\n\t\tSampleModeV = \"Clamp\"\r\n\t}}\r\n\t\r\n\tCode\r\n\t[[\r\n\t\tVS_OUTPUT_PDX_TERRAIN TerrainVertex( float2 WithinNodePos, float2 NodeOffset, float NodeScale, float2 LodDirection, float LodLerpFactor )\r\n\t\t{{\r\n\t\t\tSTerrainVertex Vertex = CalcTerrainVertex( WithinNodePos, NodeOffset, NodeScale, LodDirection, LodLerpFactor );\r\n\r\n\t\t\t#ifdef TERRAIN_FLAT_MAP_LERP\r\n\t\t\t\tVertex.WorldSpacePos.y = lerp( Vertex.WorldSpacePos.y, FlatMapHeight + {offset}, FlatMapLerp );\r\n\t\t\t#endif\r\n\t\t\t#ifdef TERRAIN_FLAT_MAP\r\n\t\t\t\tVertex.WorldSpacePos.y = FlatMapHeight + {offset};\r\n\t\t\t#endif\r\n\r\n\t\t\tVS_OUTPUT_PDX_TERRAIN Out;\r\n\t\t\tOut.WorldSpacePos = Vertex.WorldSpacePos;\r\n\r\n\t\t\tOut.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Vertex.WorldSpacePos, 1.0 ) );\r\n\t\t\tOut.ShadowProj = mul( ShadowMapTextureMatrix, float4( Vertex.WorldSpacePos, 1.0 ) );\r\n\r\n\t\t\treturn Out;\r\n\t\t}}\r\n\t\t\r\n\t\t// Copies of the pixels shader CalcHeightBlendFactors and CalcDetailUV functions\r\n\t\tfloat4 CalcHeightBlendFactors( float4 MaterialHeights, float4 MaterialFactors, float BlendRange )\r\n\t\t{{\r\n\t\t\tfloat4 Mat = MaterialHeights + MaterialFactors;\r\n\t\t\tfloat BlendStart = max( max( Mat.x, Mat.y ), max( Mat.z, Mat.w ) ) - BlendRange;\r\n\t\t\t\r\n\t\t\tfloat4 MatBlend = max( Mat - vec4( BlendStart ), vec4( 0.0 ) );\r\n\t\t\t\r\n\t\t\tfloat Epsilon = 0.00001;\r\n\t\t\treturn float4( MatBlend ) / ( dot( MatBlend, vec4( 1.0 ) ) + Epsilon );\r\n\t\t}}\r\n\t\t\r\n\t\tfloat2 CalcDetailUV( float2 WorldSpacePosXZ )\r\n\t\t{{\r\n\t\t\treturn WorldSpacePosXZ * DetailTileFactor;\r\n\t\t}}\r\n\t\t\r\n\t\t// A low spec vertex buffer version of CalculateDetails\r\n\t\tvoid CalculateDetailsLowSpec( float2 WorldSpacePosXZ, out float3 DetailDiffuse, out float4 DetailMaterial )\r\n\t\t{{\r\n\t\t\tfloat2 DetailCoordinates = WorldSpacePosXZ * WorldSpaceToDetail;\r\n\t\t\tfloat2 DetailCoordinatesScaled = DetailCoordinates * DetailTextureSize;\r\n\t\t\tfloat2 DetailCoordinatesScaledFloored = floor( DetailCoordinatesScaled );\r\n\t\t\tfloat2 DetailCoordinatesFrac = DetailCoordinatesScaled - DetailCoordinatesScaledFloored;\r\n\t\t\tDetailCoordinates = DetailCoordinatesScaledFloored * DetailTexelSize + DetailTexelSize * 0.5;\r\n\t\t\t\r\n\t\t\tfloat4 Factors = float4(\r\n\t\t\t\t(1.0 - DetailCoordinatesFrac.x) * (1.0 - DetailCoordinatesFrac.y),\r\n\t\t\t\tDetailCoordinatesFrac.x * (1.0 - DetailCoordinatesFrac.y),\r\n\t\t\t\t(1.0 - DetailCoordinatesFrac.x) * DetailCoordinatesFrac.y,\r\n\t\t\t\tDetailCoordinatesFrac.x * DetailCoordinatesFrac.y\r\n\t\t\t);\r\n\t\t\t\r\n\t\t\tfloat4 DetailIndex = PdxTex2DLod0( DetailIndexTexture, DetailCoordinates ) * 255.0;\r\n\t\t\tfloat4 DetailMask = PdxTex2DLod0( DetailMaskTexture, DetailCoordinates ) * Factors[0];\r\n\t\t\t\r\n\t\t\tfloat2 Offsets[3];\r\n\t\t\tOffsets[0] = float2( DetailTexelSize.x, 0.0 );\r\n\t\t\tOffsets[1] = float2( 0.0, DetailTexelSize.y );\r\n\t\t\tOffsets[2] = float2( DetailTexelSize.x, DetailTexelSize.y );\r\n\t\t\t\r\n\t\t\tfor ( int k = 0; k < 3; ++k )\r\n\t\t\t{{\r\n\t\t\t\tfloat2 DetailCoordinates2 = DetailCoordinates + Offsets[k];\r\n\t\t\t\t\r\n\t\t\t\tfloat4 DetailIndices = PdxTex2DLod0( DetailIndexTexture, DetailCoordinates2 ) * 255.0;\r\n\t\t\t\tfloat4 DetailMasks = PdxTex2DLod0( DetailMaskTexture, DetailCoordinates2 ) * Factors[k+1];\r\n\t\t\t\t\r\n\t\t\t\tfor ( int i = 0; i < 4; ++i )\r\n\t\t\t\t{{\r\n\t\t\t\t\tfor ( int j = 0; j < 4; ++j )\r\n\t\t\t\t\t{{\r\n\t\t\t\t\t\tif ( DetailIndex[j] == DetailIndices[i] )\r\n\t\t\t\t\t\t{{\r\n\t\t\t\t\t\t\tDetailMask[j] += DetailMasks[i];\r\n\t\t\t\t\t\t}}\r\n\t\t\t\t\t}}\r\n\t\t\t\t}}\r\n\t\t\t}}\r\n\r\n\t\t\tfloat2 DetailUV = CalcDetailUV( WorldSpacePosXZ );\r\n\t\t\t\r\n\t\t\tfloat4 DiffuseTexture0 = PdxTex2DLod0( DetailTextures, float3( DetailUV, DetailIndex[0] ) ) * smoothstep( 0.0, 0.1, DetailMask[0] );\r\n\t\t\tfloat4 DiffuseTexture1 = PdxTex2DLod0( DetailTextures, float3( DetailUV, DetailIndex[1] ) ) * smoothstep( 0.0, 0.1, DetailMask[1] );\r\n\t\t\tfloat4 DiffuseTexture2 = PdxTex2DLod0( DetailTextures, float3( DetailUV, DetailIndex[2] ) ) * smoothstep( 0.0, 0.1, DetailMask[2] );\r\n\t\t\tfloat4 DiffuseTexture3 = PdxTex2DLod0( DetailTextures, float3( DetailUV, DetailIndex[3] ) ) * smoothstep( 0.0, 0.1, DetailMask[3] );\r\n\t\t\t\r\n\t\t\tfloat4 BlendFactors = CalcHeightBlendFactors( float4( DiffuseTexture0.a, DiffuseTexture1.a, DiffuseTexture2.a, DiffuseTexture3.a ), DetailMask, DetailBlendRange );\r\n\t\t\t//BlendFactors = DetailMask;\r\n\t\t\t\r\n\t\t\tDetailDiffuse = DiffuseTexture0.rgb * BlendFactors.x + \r\n\t\t\t\t\t\t\tDiffuseTexture1.rgb * BlendFactors.y + \r\n\t\t\t\t\t\t\tDiffuseTexture2.rgb * BlendFactors.z + \r\n\t\t\t\t\t\t\tDiffuseTexture3.rgb * BlendFactors.w;\r\n\t\t\t\r\n\t\t\tDetailMaterial = vec4( 0.0 );\r\n\t\t\t\r\n\t\t\tfor ( int i = 0; i < 4; ++i )\r\n\t\t\t{{\r\n\t\t\t\tfloat BlendFactor = BlendFactors[i];\r\n\t\t\t\tif ( BlendFactor > 0.0 )\r\n\t\t\t\t{{\r\n\t\t\t\t\tfloat3 ArrayUV = float3( DetailUV, DetailIndex[i] );\r\n\t\t\t\t\tfloat4 NormalTexture = PdxTex2DLod0( NormalTextures, ArrayUV );\r\n\t\t\t\t\tfloat4 MaterialTexture = PdxTex2DLod0( MaterialTextures, ArrayUV );\r\n\r\n\t\t\t\t\tDetailMaterial += MaterialTexture * BlendFactor;\r\n\t\t\t\t}}\r\n\t\t\t}}\r\n\t\t}}\r\n\t\r\n\t\tVS_OUTPUT_PDX_TERRAIN_LOW_SPEC TerrainVertexLowSpec( float2 WithinNodePos, float2 NodeOffset, float NodeScale, float2 LodDirection, float LodLerpFactor )\r\n\t\t{{\r\n\t\t\tSTerrainVertex Vertex = CalcTerrainVertex( WithinNodePos, NodeOffset, NodeScale, LodDirection, LodLerpFactor );\r\n\r\n\t\t\t#ifdef TERRAIN_FLAT_MAP_LERP\r\n\t\t\t\tVertex.WorldSpacePos.y = lerp( Vertex.WorldSpacePos.y, FlatMapHeight, FlatMapLerp );\r\n\t\t\t#endif\r\n\t\t\t#ifdef TERRAIN_FLAT_MAP\r\n\t\t\t\tVertex.WorldSpacePos.y = FlatMapHeight;\r\n\t\t\t#endif\r\n\r\n\t\t\tVS_OUTPUT_PDX_TERRAIN_LOW_SPEC Out;\r\n\t\t\tOut.WorldSpacePos = Vertex.WorldSpacePos;\r\n\r\n\t\t\tOut.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Vertex.WorldSpacePos, 1.0 ) );\r\n\t\t\tOut.ShadowProj = mul( ShadowMapTextureMatrix, float4( Vertex.WorldSpacePos, 1.0 ) );\r\n\t\t\t\r\n\t\t\tCalculateDetailsLowSpec( Vertex.WorldSpacePos.xz, Out.DetailDiffuse, Out.DetailMaterial );\r\n\t\t\t\r\n\t\t\tfloat2 ColorMapCoords = Vertex.WorldSpacePos.xz * WorldSpaceToTerrain0To1;\r\n\r\n#if defined( PDX_OSX ) && defined( PDX_OPENGL )\r\n\t\t\t// We're limited to the amount of samplers we can bind at any given time on Mac, so instead\r\n\t\t\t// we disable the usage of ColorTexture (since its effects are very subtle) and assign a\r\n\t\t\t// default value here instead.\r\n\t\t\tOut.ColorMap = float3( vec3( 0.5 ) );\r\n#else\r\n\t\t\tOut.ColorMap = PdxTex2DLod0( ColorTexture, float2( ColorMapCoords.x, 1.0 - ColorMapCoords.y ) ).rgb;\r\n#endif\r\n\r\n\t\t\tOut.FlatMap = float3( vec3( 0.5f ) ); // neutral overlay\r\n\t\t\t#ifdef TERRAIN_FLAT_MAP_LERP\r\n\t\t\t\tOut.FlatMap = lerp( Out.FlatMap, PdxTex2DLod0( FlatMapTexture, float2( ColorMapCoords.x, 1.0 - ColorMapCoords.y ) ).rgb, FlatMapLerp );\r\n\t\t\t#endif\r\n\r\n\t\t\tOut.Normal = CalculateNormal( Vertex.WorldSpacePos.xz );\r\n\r\n\t\t\treturn Out;\r\n\t\t}}\r\n\t]]\r\n\t\r\n\tMainCode VertexShader\r\n\t{{\r\n\t\tInput = \"VS_INPUT_PDX_TERRAIN\"\r\n\t\tOutput = \"VS_OUTPUT_PDX_TERRAIN\"\r\n\t\tCode\r\n\t\t[[\r\n\t\t\tPDX_MAIN\r\n\t\t\t{{\r\n\t\t\t\treturn TerrainVertex( Input.UV, Input.NodeOffset_Scale_Lerp.xy, Input.NodeOffset_Scale_Lerp.z, Input.LodDirection, Input.NodeOffset_Scale_Lerp.w );\r\n\t\t\t}}\r\n\t\t]]\r\n\t}}\r\n\r\n\tMainCode VertexShaderSkirt\r\n\t{{\r\n\t\tInput = \"VS_INPUT_PDX_TERRAIN_SKIRT\"\r\n\t\tOutput = \"VS_OUTPUT_PDX_TERRAIN\"\r\n\t\tCode\r\n\t\t[[\r\n\t\t\tPDX_MAIN\r\n\t\t\t{{\r\n\t\t\t\tVS_OUTPUT_PDX_TERRAIN Out = TerrainVertex( Input.UV, Input.NodeOffset_Scale_Lerp.xy, Input.NodeOffset_Scale_Lerp.z, Input.LodDirection, Input.NodeOffset_Scale_Lerp.w );\r\n\r\n\t\t\t\tfloat3 Position = FixPositionForSkirt( Out.WorldSpacePos, Input.VertexID );\r\n\t\t\t\tOut.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Position, 1.0 ) );\r\n\r\n\t\t\t\treturn Out;\r\n\t\t\t}}\r\n\t\t]]\r\n\t}}\r\n\t\r\n\tMainCode VertexShaderLowSpec\r\n\t{{\r\n\t\tInput = \"VS_INPUT_PDX_TERRAIN\"\r\n\t\tOutput = \"VS_OUTPUT_PDX_TERRAIN_LOW_SPEC\"\r\n\t\tCode\r\n\t\t[[\r\n\t\t\tPDX_MAIN\r\n\t\t\t{{\r\n\t\t\t\treturn TerrainVertexLowSpec( Input.UV, Input.NodeOffset_Scale_Lerp.xy, Input.NodeOffset_Scale_Lerp.z, Input.LodDirection, Input.NodeOffset_Scale_Lerp.w );\r\n\t\t\t}}\r\n\t\t]]\r\n\t}}\r\n\r\n\tMainCode VertexShaderLowSpecSkirt\r\n\t{{\r\n\t\tInput = \"VS_INPUT_PDX_TERRAIN_SKIRT\"\r\n\t\tOutput = \"VS_OUTPUT_PDX_TERRAIN_LOW_SPEC\"\r\n\t\tCode\r\n\t\t[[\r\n\t\t\tPDX_MAIN\r\n\t\t\t{{\r\n\t\t\t\tVS_OUTPUT_PDX_TERRAIN_LOW_SPEC Out = TerrainVertexLowSpec( Input.UV, Input.NodeOffset_Scale_Lerp.xy, Input.NodeOffset_Scale_Lerp.z, Input.LodDirection, Input.NodeOffset_Scale_Lerp.w );\r\n\r\n\t\t\t\tfloat3 Position = FixPositionForSkirt( Out.WorldSpacePos, Input.VertexID );\r\n\t\t\t\tOut.Position = FixProjectionAndMul( ViewProjectionMatrix, float4( Position, 1.0 ) );\r\n\r\n\t\t\t\treturn Out;\r\n\t\t\t}}\r\n\t\t]]\r\n\t}}\r\n}}\r\n\r\n\r\nPixelShader =\r\n{{\r\n\t# PdxTerrain uses texture index 0 - 6\r\n\r\n\t# Jomini specific\r\n\tTextureSampler ShadowMap\r\n\t{{\r\n\t\tRef = PdxShadowmap\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Wrap\"\r\n\t\tSampleModeV = \"Wrap\"\r\n\t\tCompareFunction = less_equal\r\n\t\tSamplerType = \"Compare\"\r\n\t}}\r\n\r\n\t# Game specific\r\n\tTextureSampler FogOfWarAlpha\r\n\t{{\r\n\t\tRef = JominiFogOfWar\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Wrap\"\r\n\t\tSampleModeV = \"Wrap\"\r\n\t}}\r\n\tTextureSampler FlatMapTexture\r\n\t{{\r\n\t\tRef = TerrainFlatMap\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Clamp\"\r\n\t\tSampleModeV = \"Clamp\"\r\n\t}}\r\n\tTextureSampler EnvironmentMap\r\n\t{{\r\n\t\tRef = JominiEnvironmentMap\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Clamp\"\r\n\t\tSampleModeV = \"Clamp\"\r\n\t\tType = \"Cube\"\r\n\t}}\r\n\tTextureSampler FlatMapEnvironmentMap\r\n\t{{\r\n\t\tRef = FlatMapEnvironmentMap\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Clamp\"\r\n\t\tSampleModeV = \"Clamp\"\r\n\t\tType = \"Cube\"\r\n\t}}\r\n\tTextureSampler SurroundFlatMapMask\r\n\t{{\r\n\t\tRef = SurroundFlatMapMask\r\n\t\tMagFilter = \"Linear\"\r\n\t\tMinFilter = \"Linear\"\r\n\t\tMipFilter = \"Linear\"\r\n\t\tSampleModeU = \"Border\"\r\n\t\tSampleModeV = \"Border\"\r\n\t\tBorder_Color = {{ 1 1 1 1 }}\r\n\t\tFile = \"gfx/map/surround_map/surround_mask.dds\"\r\n\t}}\r\n\r\n\tCode\r\n\t[[\r\n\t\tSLightingProperties GetFlatMapLerpSunLightingProperties( float3 WorldSpacePos, float ShadowTerm )\r\n\t\t{{\r\n\t\t\tSLightingProperties LightingProps;\r\n\t\t\tLightingProps._ToCameraDir = normalize( CameraPosition - WorldSpacePos );\r\n\t\t\tLightingProps._ToLightDir = ToSunDir;\r\n\t\t\tLightingProps._LightIntensity = FlatMapLerpSunDiffuse * 5;\r\n\t\t\tLightingProps._ShadowTerm = ShadowTerm;\r\n\t\t\tLightingProps._CubemapIntensity = FlatMapLerpCubemapIntensity;\r\n\t\t\tLightingProps._CubemapYRotation = FlatMapLerpCubemapYRotation;\r\n\r\n\t\t\treturn LightingProps;\r\n\t\t}}\r\n\t]]\r\n\r\n\tMainCode PixelShader\r\n\t{{\r\n\t\tInput = \"VS_OUTPUT_PDX_TERRAIN\"\r\n\t\tOutput = \"PDX_COLOR\"\r\n\t\tCode\r\n\t\t[[\r\n\t\t\tPDX_MAIN\r\n\t\t\t{{\r\n\t\t\t\tclip( vec2(1.0) - Input.WorldSpacePos.xz * WorldSpaceToTerrain0To1 );\r\n\r\n\t\t\t\tfloat4 DetailDiffuse;\r\n\t\t\t\tfloat3 DetailNormal;\r\n\t\t\t\tfloat4 DetailMaterial;\r\n\t\t\t\tCalculateDetails( Input.WorldSpacePos.xz, DetailDiffuse, DetailNormal, DetailMaterial );\r\n\r\n\t\t\t\tfloat2 ColorMapCoords = Input.WorldSpacePos.xz * WorldSpaceToTerrain0To1;\r\n#if defined( PDX_OSX ) && defined( PDX_OPENGL )\r\n\t\t\t\t// We're limited to the amount of samplers we can bind at any given time on Mac, so instead\r\n\t\t\t\t// we disable the usage of ColorTexture (since its effects are very subtle) and assign a\r\n\t\t\t\t// default value here instead.\r\n\t\t\t\tfloat3 ColorMap = float3( vec3( 0.5 ) );\r\n#else\r\n\t\t\t\tfloat3 ColorMap = PdxTex2D( ColorTexture, float2( ColorMapCoords.x, 1.0 - ColorMapCoords.y ) ).rgb;\r\n#endif\r\n\t\t\t\t\r\n\t\t\t\tfloat3 FlatMap = float3( vec3( 0.5f ) ); // neutral overlay\r\n\t\t\t\t#ifdef TERRAIN_FLAT_MAP_LERP\r\n\t\t\t\t\tFlatMap = lerp( FlatMap, PdxTex2D( FlatMapTexture, float2( ColorMapCoords.x, 1.0 - ColorMapCoords.y ) ).rgb, FlatMapLerp );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tfloat3 Normal = CalculateNormal( Input.WorldSpacePos.xz );\r\n\r\n\t\t\t\tfloat3 ReorientedNormal = ReorientNormal( Normal, DetailNormal );\r\n\r\n\t\t\t\tfloat SnowHighlight = 0.0f;\r\n\t\t\t\t#ifndef UNDERWATER\r\n\t\t\t\t\tDetailDiffuse.rgb = ApplyDynamicMasksDiffuse( DetailDiffuse.rgb, ReorientedNormal, ColorMapCoords );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tfloat3 Diffuse = GetOverlay( DetailDiffuse.rgb, ColorMap, ( 1 - DetailMaterial.r ) * COLORMAP_OVERLAY_STRENGTH );\r\n\r\n\r\n\t\t\t\t#ifdef TERRAIN_COLOR_OVERLAY\r\n\t\t\t\t\tfloat3 BorderColor;\r\n\t\t\t\t\tfloat BorderPreLightingBlend;\r\n\t\t\t\t\tfloat BorderPostLightingBlend;\r\n\t\t\t\t\tGetBorderColorAndBlendGame( Input.WorldSpacePos.xz, FlatMap, BorderColor, BorderPreLightingBlend, BorderPostLightingBlend );\r\n\r\n\t\t\t\t\tDiffuse = lerp( Diffuse, BorderColor, BorderPreLightingBlend );\r\n\r\n\t\t\t\t\t#ifdef TERRAIN_FLAT_MAP_LERP\r\n\t\t\t\t\t\tfloat3 FlatColor;\r\n\t\t\t\t\t\tGetBorderColorAndBlendGameLerp( Input.WorldSpacePos.xz, FlatMap, FlatColor, BorderPreLightingBlend, BorderPostLightingBlend, FlatMapLerp );\r\n\t\t\t\t\t\t\r\n\t\t\t\t\t\tFlatMap = lerp( FlatMap, FlatColor, saturate( BorderPreLightingBlend + BorderPostLightingBlend ) );\r\n\t\t\t\t\t#endif\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_COLOR_OVERLAY\r\n\t\t\t\t\tApplyHighlightColor( Diffuse, ColorMapCoords );\r\n\t\t\t\t\tCompensateWhiteHighlightColor( Diffuse, ColorMapCoords, SnowHighlight );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tfloat ShadowTerm = CalculateShadow( Input.ShadowProj, ShadowMap );\r\n\r\n\t\t\t\t#ifdef TERRAIN_FLAT_MAP_LERP\r\n\t\t\t\tif ( HasFlatMapLightingEnabled == 1 )\r\n\t\t\t\t{{\r\n \t\t\t\t\tSMaterialProperties FlatMapMaterialProps = GetMaterialProperties( FlatMap, float3( 0.0, 1.0, 0.0 ), 1.0, 0.0, 0.0 );\r\n \t\t\t\t\tSLightingProperties FlatMapLightingProps = GetFlatMapLerpSunLightingProperties( Input.WorldSpacePos, ShadowTerm );\r\n \t\t\t\t\tFlatMap = CalculateSunLighting( FlatMapMaterialProps, FlatMapLightingProps, FlatMapEnvironmentMap );\r\n\t\t\t\t}}\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tSMaterialProperties MaterialProps = GetMaterialProperties( Diffuse, ReorientedNormal, DetailMaterial.a, DetailMaterial.g, DetailMaterial.b );\r\n\t\t\t\tSLightingProperties LightingProps = GetSunLightingProperties( Input.WorldSpacePos, ShadowTerm );\r\n\r\n\t\t\t\tfloat3 FinalColor = CalculateSunLighting( MaterialProps, LightingProps, EnvironmentMap );\r\n\r\n\t\t\t\t#ifdef TERRAIN_COLOR_OVERLAY\r\n\t\t\t\t\tFinalColor.rgb = lerp( FinalColor.rgb, BorderColor, BorderPostLightingBlend );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_COLOR_OVERLAY\r\n\t\t\t\t\tApplyHighlightColor( FinalColor.rgb, ColorMapCoords, 0.25 );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_COLOR_OVERLAY\r\n\t\t\t\t\tApplyDiseaseDiffuse( FinalColor, ColorMapCoords );\r\n\t\t\t\t\tApplyLegendDiffuse( FinalColor, ColorMapCoords );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifndef UNDERWATER\r\n\t\t\t\t\tFinalColor = ApplyFogOfWar( FinalColor, Input.WorldSpacePos, FogOfWarAlpha );\r\n\t\t\t\t\tFinalColor = ApplyDistanceFog( FinalColor, Input.WorldSpacePos );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_FLAT_MAP_LERP\r\n\t\t\t\t\tFinalColor = lerp( FinalColor, FlatMap, FlatMapLerp );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tfloat Alpha = 1.0;\r\n\t\t\t\t#ifdef UNDERWATER\r\n\t\t\t\t\tAlpha = CompressWorldSpace( Input.WorldSpacePos );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_DEBUG\r\n\t\t\t\t\tTerrainDebug( FinalColor, Input.WorldSpacePos );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tDebugReturn( FinalColor, MaterialProps, LightingProps, EnvironmentMap );\r\n\t\t\t\treturn float4( FinalColor, Alpha );\r\n\t\t\t}}\r\n\t\t]]\r\n\t}}\r\n\r\n\tMainCode PixelShaderLowSpec\r\n\t{{\r\n\t\tInput = \"VS_OUTPUT_PDX_TERRAIN_LOW_SPEC\"\r\n\t\tOutput = \"PDX_COLOR\"\r\n\t\tCode\r\n\t\t[[\r\n\t\t\tPDX_MAIN\r\n\t\t\t{{\r\n\t\t\t\tclip( vec2(1.0) - Input.WorldSpacePos.xz * WorldSpaceToTerrain0To1 );\r\n\r\n\t\t\t\tfloat3 DetailDiffuse = Input.DetailDiffuse;\r\n\t\t\t\tfloat4 DetailMaterial = Input.DetailMaterial;\r\n\r\n\t\t\t\tfloat2 ColorMapCoords = Input.WorldSpacePos.xz * WorldSpaceToTerrain0To1;\r\n\r\n\t\t\t\tfloat3 ColorMap = Input.ColorMap;\r\n\t\t\t\tfloat3 FlatMap = Input.FlatMap;\r\n\r\n\t\t\t\tfloat3 Normal = Input.Normal;\r\n\t\t\t\t\r\n\t\t\t\tfloat SnowHighlight = 0.0f;\r\n\t\t\t\t#ifndef UNDERWATER\r\n\t\t\t\t\tDetailDiffuse = ApplyDynamicMasksDiffuse( DetailDiffuse, Normal, ColorMapCoords );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tfloat3 Diffuse = GetOverlay( DetailDiffuse.rgb, ColorMap, ( 1 - DetailMaterial.r ) * COLORMAP_OVERLAY_STRENGTH );\r\n\t\t\t\tfloat3 ReorientedNormal = Normal;\r\n\r\n\t\t\t\t#ifdef TERRAIN_COLOR_OVERLAY\r\n\t\t\t\t\tfloat3 BorderColor;\r\n\t\t\t\t\tfloat BorderPreLightingBlend;\r\n\t\t\t\t\tfloat BorderPostLightingBlend;\r\n\t\t\t\t\tGetBorderColorAndBlendGame( Input.WorldSpacePos.xz, FlatMap, BorderColor, BorderPreLightingBlend, BorderPostLightingBlend );\r\n\r\n\t\t\t\t\tDiffuse = lerp( Diffuse, BorderColor, BorderPreLightingBlend );\r\n\r\n\t\t\t\t\t#ifdef TERRAIN_FLAT_MAP_LERP\r\n\t\t\t\t\t\tfloat3 FlatColor;\r\n\t\t\t\t\t\tGetBorderColorAndBlendGameLerp( Input.WorldSpacePos.xz, FlatMap, FlatColor, BorderPreLightingBlend, BorderPostLightingBlend, FlatMapLerp );\r\n\t\t\t\t\t\t\r\n\t\t\t\t\t\tFlatMap = lerp( FlatMap, FlatColor, saturate( BorderPreLightingBlend + BorderPostLightingBlend ) );\r\n\t\t\t\t\t#endif \r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t//float ShadowTerm = CalculateShadow( Input.ShadowProj, ShadowMap );\r\n\t\t\t\tfloat ShadowTerm = 1.0;\r\n\r\n\t\t\t\tSMaterialProperties MaterialProps = GetMaterialProperties( Diffuse, ReorientedNormal, DetailMaterial.a, DetailMaterial.g, DetailMaterial.b );\r\n\t\t\t\tSLightingProperties LightingProps = GetSunLightingProperties( Input.WorldSpacePos, ShadowTerm );\r\n\r\n\t\t\t\tfloat3 FinalColor = CalculateSunLightingLowSpec( MaterialProps, LightingProps );\r\n\r\n\t\t\t\t#ifndef UNDERWATER\r\n\t\t\t\t\tFinalColor = ApplyFogOfWar( FinalColor, Input.WorldSpacePos, FogOfWarAlpha );\r\n\t\t\t\t\tFinalColor = ApplyDistanceFog( FinalColor, Input.WorldSpacePos );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_COLOR_OVERLAY\r\n\t\t\t\t\tFinalColor.rgb = lerp( FinalColor.rgb, BorderColor, BorderPostLightingBlend );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_COLOR_OVERLAY\r\n\t\t\t\t\tApplyHighlightColor( FinalColor.rgb, ColorMapCoords );\r\n\t\t\t\t\tCompensateWhiteHighlightColor( FinalColor.rgb, ColorMapCoords, SnowHighlight );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_FLAT_MAP_LERP\r\n\t\t\t\t\tFinalColor = lerp( FinalColor, FlatMap, FlatMapLerp );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tfloat Alpha = 1.0;\r\n\t\t\t\t#ifdef UNDERWATER\r\n\t\t\t\t\tAlpha = CompressWorldSpace( Input.WorldSpacePos );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_DEBUG\r\n\t\t\t\t\tTerrainDebug( FinalColor, Input.WorldSpacePos );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tDebugReturn( FinalColor, MaterialProps, LightingProps, EnvironmentMap );\r\n\t\t\t\treturn float4( FinalColor, Alpha );\r\n\t\t\t}}\r\n\t\t]]\r\n\t}}\r\n\r\n\tMainCode PixelShaderFlatMap\r\n\t{{\r\n\t\tInput = \"VS_OUTPUT_PDX_TERRAIN\"\r\n\t\tOutput = \"PDX_COLOR\"\r\n\t\tCode\r\n\t\t[[\r\n\t\t\tPDX_MAIN\r\n\t\t\t{{\r\n\t\t\t\t#ifdef TERRAIN_SKIRT\r\n\t\t\t\t\treturn float4( 0, 0, 0, 0 );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tclip( vec2(1.0) - Input.WorldSpacePos.xz * WorldSpaceToTerrain0To1 );\r\n\r\n\t\t\t\tfloat2 ColorMapCoords = Input.WorldSpacePos.xz * WorldSpaceToTerrain0To1;\r\n\t\t\t\tfloat3 FlatMap = PdxTex2D( FlatMapTexture, float2( ColorMapCoords.x, 1.0 - ColorMapCoords.y ) ).rgb;\r\n\r\n\t\t\t\t#ifdef TERRAIN_COLOR_OVERLAY\r\n\t\t\t\t\tfloat3 BorderColor;\r\n\t\t\t\t\tfloat BorderPreLightingBlend;\r\n\t\t\t\t\tfloat BorderPostLightingBlend;\r\n\t\t\t\t\t\r\n\t\t\t\t\tGetBorderColorAndBlendGameLerp( Input.WorldSpacePos.xz, FlatMap, BorderColor, BorderPreLightingBlend, BorderPostLightingBlend, 1.0f );\r\n\t\t\t\t\t\r\n\t\t\t\t\tFlatMap = lerp( FlatMap, BorderColor, saturate( BorderPreLightingBlend + BorderPostLightingBlend ) );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\tfloat3 FinalColor = FlatMap;\r\n\t\t\t\t#ifdef TERRAIN_FLATMAP_LIGHTING\r\n\t\t\t\t\tif ( HasFlatMapLightingEnabled == 1 )\r\n\t\t\t\t\t{{\r\n\t\t\t\t\t\tfloat ShadowTerm = CalculateShadow( Input.ShadowProj, ShadowMap );\r\n\t\t\t\t\t\tSMaterialProperties FlatMapMaterialProps = GetMaterialProperties( FlatMap, float3( 0.0, 1.0, 0.0 ), 1.0, 0.0, 0.0 );\r\n\t\t\t\t\t\tSLightingProperties FlatMapLightingProps = GetSunLightingProperties( Input.WorldSpacePos, ShadowTerm );\r\n\t\t\t\t\t\tFinalColor = CalculateSunLighting( FlatMapMaterialProps, FlatMapLightingProps, EnvironmentMap );\r\n\t\t\t\t\t}}\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_COLOR_OVERLAY\r\n\t\t\t\t\tApplyHighlightColor( FinalColor, ColorMapCoords, 0.5 );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t#ifdef TERRAIN_DEBUG\r\n\t\t\t\t\tTerrainDebug( FinalColor, Input.WorldSpacePos );\r\n\t\t\t\t#endif\r\n\r\n\t\t\t\t// Make flatmap transparent based on the SurroundFlatMapMask\r\n\t\t\t\tfloat SurroundMapAlpha = 1 - PdxTex2D( SurroundFlatMapMask, float2( ColorMapCoords.x, 1.0 - ColorMapCoords.y ) ).b;\r\n\t\t\t\tSurroundMapAlpha *= FlatMapLerp;\r\n\r\n\t\t\t\treturn float4( FinalColor, SurroundMapAlpha );\r\n\t\t\t}}\r\n\t\t]]\r\n\t}}\r\n}}\r\n\r\n\r\nEffect PdxTerrain\r\n{{\r\n\tVertexShader = \"VertexShader\"\r\n\tPixelShader = \"PixelShader\"\r\n\r\n\tDefines = {{ \"TERRAIN_FLAT_MAP_LERP\" }}\r\n}}\r\n\r\nEffect PdxTerrainLowSpec\r\n{{\r\n\tVertexShader = \"VertexShaderLowSpec\"\r\n\tPixelShader = \"PixelShaderLowSpec\"\r\n}}\r\n\r\nEffect PdxTerrainSkirt\r\n{{\r\n\tVertexShader = \"VertexShaderSkirt\"\r\n\tPixelShader = \"PixelShader\"\r\n}}\r\n\r\nEffect PdxTerrainLowSpecSkirt\r\n{{\r\n\tVertexShader = \"VertexShaderLowSpecSkirt\"\r\n\tPixelShader = \"PixelShaderLowSpec\"\r\n}}\r\n\r\n### FlatMap Effects\r\n\r\nBlendState BlendStateAlpha\r\n{{\r\n\tBlendEnable = yes\r\n\tSourceBlend = \"SRC_ALPHA\"\r\n\tDestBlend = \"INV_SRC_ALPHA\"\r\n}}\r\n\r\nEffect PdxTerrainFlat\r\n{{\r\n\tVertexShader = \"VertexShader\"\r\n\tPixelShader = \"PixelShaderFlatMap\"\r\n\tBlendState = BlendStateAlpha\r\n\r\n\tDefines = {{ \"TERRAIN_FLAT_MAP\" \"TERRAIN_FLATMAP_LIGHTING\" }}\r\n}}\r\n\r\nEffect PdxTerrainFlatSkirt\r\n{{\r\n\tVertexShader = \"VertexShaderSkirt\"\r\n\tPixelShader = \"PixelShaderFlatMap\"\r\n\tBlendState = BlendStateAlpha\r\n\r\n\tDefines = {{ \"TERRAIN_FLAT_MAP\" \"TERRAIN_SKIRT\" }}\r\n}}\r\n\r\n# Low Spec flat map the same as regular effect\r\nEffect PdxTerrainFlatLowSpec\r\n{{\r\n\tVertexShader = \"VertexShader\"\r\n\tPixelShader = \"PixelShaderFlatMap\"\r\n\tBlendState = BlendStateAlpha\r\n\r\n\tDefines = {{ \"TERRAIN_FLAT_MAP\" }}\r\n}}\r\n\r\nEffect PdxTerrainFlatLowSpecSkirt\r\n{{\r\n\tVertexShader = \"VertexShaderSkirt\"\r\n\tPixelShader = \"PixelShaderFlatMap\"\r\n\tBlendState = BlendStateAlpha\r\n\r\n\tDefines = {{ \"TERRAIN_FLAT_MAP\" \"TERRAIN_SKIRT\" }}\r\n}}\r\n";
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
        File.WriteAllText(path, file);
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
