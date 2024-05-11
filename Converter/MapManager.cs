using ImageMagick;
using Microsoft.VisualBasic.FileIO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Converter;

// These are needed for AOT compilation.
[JsonSerializable(typeof(GeoMap))]
public partial class GeoMapJsonContext : JsonSerializerContext {}

[JsonSerializable(typeof(PackProvince))]
[JsonSerializable(typeof(JsonMap))]
public partial class JsonMapJsonContext : JsonSerializerContext {}

public static class MapManager
{
    private const int WaterLevelHeight = 30;

    //public static string OutputDirectory { get; set; } = $"{Environment.CurrentDirectory}/mod";
    public static string OutputDirectory => $"{SettingsManager.Settings.modsDirectory}/{SettingsManager.Settings.modName}";

 
    public static async Task<GeoMap> LoadGeojson()
    {
        try
        {
            var file = await File.ReadAllTextAsync("input.geojson");
            var geomap = JsonSerializer.Deserialize(file, GeoMapJsonContext.Default.GeoMap);
            return geomap;
        }
        catch (Exception e)
        {
            Debugger.Break();
            throw;
        }
    }
    //public static async Task<GeoMapRivers> LoadGeojsonRivers()
    //{
    //    try
    //    {
    //        var file = await File.ReadAllTextAsync("inputRivers.geojson");
    //        var geomap = JsonSerializer.Deserialize<GeoMapRivers>(file);
    //        return geomap;
    //    }
    //    catch (Exception e)
    //    {
    //        Debugger.Break();
    //        throw;
    //    }
    //}
    public static async Task<JsonMap> LoadJson()
    {
        try
        {
            var file = await File.ReadAllTextAsync("input.json");
            var jsonmap = JsonSerializer.Deserialize(file, JsonMapJsonContext.Default.JsonMap);
            return jsonmap;
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
        try
        {
            var cells = waterProvince.Cells;

            var largestWaterProvince = cells.MaxBy(n => n.area).area;
            var areaPerProvince = largestWaterProvince / 2;

            var unprocessedCells = waterProvince.Cells.ToDictionary(n => n.id, n => n);
            var provinces = new List<Province>();

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

                        foreach (var n in currentCell.neighbors.Where(n => unprocessedCells.ContainsKey(n)))
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

            return provinces;
        }
        catch (Exception ex)
        {
            Debugger.Break();
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

    private static PointD GeoToPixel(float lon, float lat, Map map)
    {
        return new PointD((lon - map.XOffset) * map.XRatio, Map.MapHeight - (lat - map.YOffset) * map.YRatio);
    }
    private static PointD GeoToPixelCrutch(float lon, float lat, Map map)
    {
        return new PointD((lon - map.XOffset) * map.XRatio, (lat - map.YOffset) * map.YRatio);
    }
    private static PointD PixelToFullPixel(float x, float y, Map map)
    {
        return new PointD(x * map.pixelXRatio, Map.MapHeight - y * map.pixelYRatio);
    }

    public static async Task<Map> ConvertMap(GeoMap geoMap, GeoMapRivers geoMapRivers, JsonMap jsonMap)
    {
        var provinces = CreateProvinces(geoMap, jsonMap);

        var rnd = new Random(1);
        var nameBase = jsonMap.nameBases[rnd.Next(jsonMap.nameBases.Length)];
        var nameBaseNames = nameBase.b.Split(',')
            .Select(n => 
            {
                var id = n.Replace("'", "").Replace(' ', '_').ToLowerInvariant();
                return new NameBaseName(id, n);
            }).ToArray();

        var map = new Map
        {
            GeoMap = geoMap,
            Rivers = geoMapRivers,
            JsonMap = jsonMap,
            Provinces = provinces,
            IdToIndex = provinces.Select((n, i) => (n, i)).ToDictionary(n => n.n.Id, n => n.i),
            NameBase = new NameBasePrepared(nameBase.name, nameBaseNames),
        };

        return map;
    }

    public static async Task DrawCells(Map map)
    {
        try
        {
            var settings = new MagickReadSettings()
            {
                Width = Map.MapWidth,
                Height = Map.MapHeight,
            };
            using var cellsMap = new MagickImage("xc:white", settings);

            var drawables = new Drawables();
            foreach (var feature in map.GeoMap.features)
            {
                foreach (var cell in feature.geometry.coordinates)
                {
                    drawables
                        .DisableStrokeAntialias()
                        .StrokeWidth(2)
                        .StrokeColor(MagickColors.Black)
                        .FillOpacity(new Percentage(0))
                        .Polygon(cell.Select(n => GeoToPixel(n[0], n[1], map)));
                }
            }

            cellsMap.Draw(drawables);
            await cellsMap.WriteAsync($"{Environment.CurrentDirectory}/cells.png");
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
                Width = Map.MapWidth,
                Height = Map.MapHeight,
            };
            using var cellsMap = new MagickImage("xc:black", settings);

            var drawables = new Drawables();
            foreach (var province in map.Provinces.Skip(1))
            {
                foreach (var cell in province.Cells)
                {
                    drawables
                        .DisableStrokeAntialias()
                        .StrokeColor(province.Color)
                        .FillColor(province.Color)
                        .Polygon(cell.cells.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, Map.MapHeight - (n[1] - map.YOffset) * map.YRatio)));
                }
            }

            cellsMap.Draw(drawables);
            var path = $"{OutputDirectory}/map_data/provinces.png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await cellsMap.WriteAsync(path);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }
    public static async Task DrawHeightMap(Map map)
    {
        try
        {
            var settings = new MagickReadSettings()
            {
                Width = Map.MapWidth,
                Height = Map.MapHeight,
            };
            using var cellsMap = new MagickImage("xc:black", settings);

            var drawables = new Drawables();

            var waterPackCells = map.JsonMap.pack.cells.Where(n => n.biome != 0);
            var waterGeoCells = map.GeoMap.features.Select(n => new
            {
                Height = n.properties.height,
                Id = n.properties.id,
                C = n.geometry.coordinates
            }).ToDictionary(n => n.Id, n => n);
            var cells = waterPackCells.Select(n =>
            {
                var c = waterGeoCells[n.i];
                return new
                {
                    Cells = c.C,
                    c.Height,
                };
            }).ToArray();
            var maxHeight = cells.MaxBy(n => n.Height)!.Height;

            foreach (var cellPack in cells)
            {
                foreach (var cell in cellPack.Cells)
                {
                    var trimmedHeight = cellPack.Height * (255 - WaterLevelHeight) / maxHeight + WaterLevelHeight;
                    var culledHeight = (byte)trimmedHeight;

                    var color = new MagickColor(culledHeight, culledHeight, culledHeight);
                    drawables
                        .DisableStrokeAntialias()
                        .StrokeColor(color)
                        .FillColor(color)
                        .Polygon(cell.Select(n => GeoToPixel(n[0], n[1], map)));
                }
            }

            cellsMap.Draw(drawables);
            var path = $"{OutputDirectory}/map_data/heightmap.png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await cellsMap.WriteAsync(path);

            using var file = await Image.LoadAsync(path);
            file.Mutate(n => n.GaussianBlur(15));
            file.Save(path);

        }
        catch (Exception ex)
        {

        }
    }

    public static async Task DrawRivers(Map map)
    {
        try
        {
            var settings = new MagickReadSettings()
            {
                Width = Map.MapWidth,
                Height = Map.MapHeight,
            };
            using var cellsMap = new MagickImage("xc:#ff0080", settings);

            var drawables = new Drawables();
            // Draw land
            foreach (var province in map.Provinces.Skip(1).Where(n => !n.IsWater))
            {
                foreach (var cell in province.Cells)
                {
                    drawables
                        .DisableStrokeAntialias()
                        .StrokeColor(MagickColors.White)
                        .FillColor(MagickColors.White)
                        .Polygon(cell.cells.Select(n => GeoToPixel(n[0], n[1], map)));
                }
            }

            foreach (var river in map.Rivers.features)
            {
                drawables
                    .DisableStrokeAntialias()
                    .StrokeColor(new MagickColor("#00E1FF"))
                    .StrokeWidth(0.5)
                    .Polyline(river.geometry.coordinates.Select(n => GeoToPixel(n[0], n[1], map)));
            }

            cellsMap.Draw(drawables);
            var path = $"{OutputDirectory}/map_data/rivers.png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            cellsMap.Settings.SetDefine("png:color-type", "1");

            string[] colormap = new string[] {
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
            };

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
        var lines = map.Provinces.Select((n, i) => $"{i};{n.Color.R};{n.Color.G};{n.Color.B};{n.Name};x;");
        var path = $"{OutputDirectory}/map_data/definition.csv";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllLinesAsync(path, lines);
    }

    private static async Task WriteBuildingLocators(Map map)
    {
        var offset = new PointD(0);

        var lines = map.Provinces.Where(n => n.Burg is not null).Select(n =>
        {
            var p = PixelToFullPixel(n.Burg.x, n.Burg.y, map);
            var str =
$@"        {{
            id = {map.IdToIndex[n.Id]}
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
            var path = $"{OutputDirectory}/gfx/map/map_object_data/building_locators.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
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
        var lines = map.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var p = PixelToFullPixel(n.Burg.x, n.Burg.y, map);

            var str =
$@"        {{
            id = {map.IdToIndex[n.Id]}
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
            var path = $"{OutputDirectory}/gfx/map/map_object_data/siege_locators.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
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
        var lines = map.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var p = PixelToFullPixel(n.Burg.x, n.Burg.y, map);

            var str =
$@"        {{
            id = {map.IdToIndex[n.Id]}
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
            var path = $"{OutputDirectory}/gfx/map/map_object_data/combat_locators.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
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
        var lines = map.Provinces.Skip(1).Select((n, i) =>
        {
            var p = new PointD();
            if (n.Burg is null)
            {
                var maxLon = n.Cells.SelectMany(n => n.cells).MaxBy(n => n[0])[0];
                var minLon = n.Cells.SelectMany(n => n.cells).MinBy(n => n[0])[0];

                var maxLat = n.Cells.SelectMany(n => n.cells).MaxBy(n => n[1])[1];
                var minLat = n.Cells.SelectMany(n => n.cells).MinBy(n => n[1])[1];

                p = GeoToPixelCrutch((maxLon + minLon) / 2, (maxLat + minLat) / 2, map);
            }
            else
            {
                p = PixelToFullPixel(n.Burg.x, n.Burg.y, map);
            }

            var str =
$@"        {{
            id = {map.IdToIndex[n.Id]}
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
            var path = $"{OutputDirectory}/gfx/map/map_object_data/player_stack_locators.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
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
        var waterProvinces = map.Provinces.Select((n, i) => (n, i)).Where(n => n.n.IsWater).Select(n => n.i);
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

        var path = $"{OutputDirectory}/map_data/default.map";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file);
    }

    // Town Biomes
    public static async Task WriteTerrain(Map map)
    {
        try
        {
            var provinceBiomes = map.Provinces
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
            var path = $"{OutputDirectory}/common/province_terrain/00_province_terrain.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
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
            using var cellsMap = new MagickImage("template_mask.png");

            var drawables = new Drawables();
            foreach (var cell in cells.Select(n => n.cells))
            {
                drawables
                    .DisableStrokeAntialias()
                    .StrokeColor(MagickColors.White)
                    .FillColor(MagickColors.White)
                    .Polygon(cell.Select(n => GeoToPixel(n[0], n[1], map)));
            }

            cellsMap.Draw(drawables);

            var path = $"{OutputDirectory}/gfx/map/terrain/{filename}.png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
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
        var nonWaterProvinceCells = map.Provinces
            .Skip(1)
            .Where(n => !n.IsWater && n.Cells.Any())
            .SelectMany(n => n.Cells)
            .ToArray();

        var provinceBiomes = map.Provinces
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

        var tasks = new[]
        {
            // drylands
            WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "drylands"), map, "drylands_01_mask"),
            // taiga
            WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is var b && (b == "taiga" || b == "drylands" && Helper.IsCellLowMountains(n.height) || b == "drylands" && Helper.IsCellMountains(n.height))), map, "forest_pine_01_mask"),
            // plains
            WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "plains"), map, "plains_01_mask"),
            // farmlands
            WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "farmlands"), map, "farmland_01_mask"),
            // Desert
            WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "desert" && !Helper.IsCellMountains(n.height) && !Helper.IsCellHighMountains(n.height)), map, "desert_01_mask"),
            // desert_mountains
            WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "desert" && Helper.IsCellMountains(n.height)), map, "mountain_02_desert_mask"),
            // oasis
            WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "oasis"), map, "oasis_mask"),
             // hills
            WriteMask(nonWaterProvinceCells.Where(n => Helper.IsCellHills(n.biome, n.height)), map, "hills_01_mask"),
            // low mountains
            WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellLowMountains(n.height)), map, "mountain_02_mask"),
            // mountains
            WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellMountains(n.height) || Helper.IsCellHighMountains(n.height)), map, "mountain_02_snow_mask"),
            // HighMountains
            WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellHighMountains(n.height)), map, "mountain_02_c_snow_mask"),
        };

        await Task.WhenAll(tasks);

        // jungle
        {
            var cells = nonWaterProvinceCells
                .Where(n => Helper.MapBiome(n.biome) == "jungle");

            await WriteMask(cells, map, "forest_jungle_01_mask");
        }
        // forest
        {
            var cells = nonWaterProvinceCells
                .Where(n => Helper.MapBiome(n.biome) == "forest");

            await WriteMask(cells, map, "forest_leaf_01_mask");
        }

        // wetlands
        {
            var cells = provinceBiomes.Where(n => n.Biome == "wetlands").SelectMany(n => n.Province.Cells).Where(n => Helper.MapBiome(n.biome) == "floodplains");

            await WriteMask(cells, map, "wetlands_02_mask");
        }
        // steppe
        {
            var cells = nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "steppe");

            await WriteMask(cells, map, "steppe_01_mask");
        }
        // floodplains
        {
            var cells = nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "floodplains");

            await WriteMask(cells, map, "floodplains_01_mask");
        }

    }

    public static async Task WriteGraphics()
    {
        var path = $"{OutputDirectory}/common/defines/graphic/00_graphics.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.Copy("00_graphics.txt", path, true);
    }


    private static async Task<(Dictionary<int, int> baronyCultures, string[] toOriginalCultureName)> GetCultures(Map map)
    {
        var originalCultureNames = await File.ReadAllLinesAsync("originalCultures.txt");
        var baronyCultures = map.Empires
            .SelectMany(n => n.kingdoms)
            .SelectMany(n => n.duchies)
            .SelectMany(n => n.counties)
            .SelectMany(n => n.baronies)
            .ToDictionary(n => n.id, n => n.province.Cells.Select(m => m.culture).Max());

        var totalCultures = map.JsonMap.pack.cultures.Length;
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
        var baronyReligions = map.Empires
            .SelectMany(n => n.kingdoms)
            .SelectMany(n => n.duchies)
            .SelectMany(n => n.counties)
            .SelectMany(n => n.baronies)
            .ToDictionary(n => n.id, n => n.province.Cells.Select(m => m.religion).Max());

        var totalReligions = map.JsonMap.pack.religions.Length;
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

        foreach (var empire in map.Empires)
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
        foreach (var empire in map.Empires)
        {
            foreach (var kingdom in empire.kingdoms)
            {
                var baronies = kingdom.duchies
                    .SelectMany(n => n.counties)
                    .SelectMany(n => n.baronies)
                    .Select(n =>
                    {
                        var str = $@"{map.IdToIndex[n.province.Id]} = {{
    culture = {n.Culture}
    religion = {n.Religion}
    holding = auto
}}";
                        return str;
                    })
                    .ToArray();

                var file = string.Join('\n', baronies);
                var path = $"{OutputDirectory}/history/provinces/k_{kingdom.id}.txt";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                await File.WriteAllTextAsync(path, file);
            }
        }

        // Make original culture history empty
        // Otherwise it will override newly created culture
        var originalProvincesPath = $"{map.Settings.ck3Directory}/history/provinces";
        var provincesPath = $"{OutputDirectory}/history/provinces/";
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
            File.Delete($"{OutputDirectory}/common/religion/religions/01_vanilla.txt");
        }
        catch
        {
            // Do nothing.
        }

        var religionsPath = $"{map.Settings.ck3Directory}/common/religion/religions";
        FileSystem.CopyDirectory(religionsPath, $"{OutputDirectory}/common/religion/religions", true);
    }
    // Maps original holy sites to newly created provinces.
    public static async Task WriteHolySites(Map map, string[] pickedFaiths)
    {
        var originalFaiths = (await ConfigReader.GetCK3Religions(map.Settings)).SelectMany(n => n.faiths).Where(n => pickedFaiths.Contains(n.name)).ToArray();
        var originalHolySites = await ConfigReader.GetCK3HolySites(map.Settings);

        var pickedHolySites = pickedFaiths
            .SelectMany(n => originalFaiths.First(m => m.name == n).holySites.Select(m => (name: m, holySite: originalHolySites[m])))
            .ToArray();

        var counties = map.Empires.SelectMany(n => n.kingdoms).SelectMany(n => n.duchies).SelectMany(n => n.counties).ToArray();

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
        var path = $"{OutputDirectory}/common/religion/holy_sites/00_holy_sites.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file);
    }
}
