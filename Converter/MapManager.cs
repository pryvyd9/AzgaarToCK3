using ImageMagick;
using Microsoft.VisualBasic.FileIO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Converter;

// These are needed for AOT compilation.
[JsonSerializable(typeof(GeoMap))]
public partial class GeoMapJsonContext : JsonSerializerContext { }

[JsonSerializable(typeof(PackProvince))]
[JsonSerializable(typeof(JsonMap))]
public partial class JsonMapJsonContext : JsonSerializerContext { }

public static class MapManager
{
    private const int WaterLevelHeight = 30;



    public static async Task<GeoMap> LoadGeojson()
    {
        try
        {
            var file = await File.ReadAllTextAsync(Settings.Instance.InputGeojsonPath);
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
            var file = await File.ReadAllTextAsync(Settings.Instance.InputJsonPath);
            var jsonmap = JsonSerializer.Deserialize(file, JsonMapJsonContext.Default.JsonMap);
            return jsonmap;
        }
        catch (Exception e)
        {
            Debugger.Break();
            throw;
        }

    }
    /// <summary>
    /// Generate a unique color for i along the range of 0 to maxI.
    /// </summary>
    /// <param name="i"> Must be less than maxI</param>
    /// <param name="maxI"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    private static MagickColor GetColor(int i, int maxI)
    {
        if (maxI >= 16777216)
        {
            throw new FormatException("MaxI is too big. MaxI must be less than 16777216, to ensure that the color is unique for each i");
        }
        if (i < 0 || i >= maxI)
        {
            throw new FormatException("i must be between 0 and maxI");
        }


        // max 24bit color
        const int maxColor = 256 * 256 * 256;
        var color = maxColor / maxI * i;

        byte r = (byte)((color & 0x0000FF) >> 0);
        byte g = (byte)((color & 0x00FF00) >> 8);
        byte b = (byte)((color & 0xFF0000) >> 16);

        var c = new MagickColor(r, g, b);

        return c;
    }


    private static List<IProvince> CreateWaterProvinces(List<Cell> cells)
    {

        //Agorithm overview:
        // 1. Make sure that list we are working with only contain water cells.
        // 2. Then select a random cell and grow to the desired water province size (int representing size in cells)
        //  -. Find all neighbors of the current cell that are water cells and add them to the potential water province.
        //  - Continue untill there are no more cells to grow to or the desired size is reached.
        // 3. If the desired size was not reached then we have a diminutive water province. If it is above half the desired size then we will keep it.
        // - If it is below half the desired size then we will merge it with the nearest water province. (or if that fails keep it)
        // 4. Repeat untill all water cells are in a water province.




        



        var waterCells = cells.Where(n => n.Biome == 0).ToList();

        try
        {

        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }

    /// <summary>
    /// Province is a shared term between Azgaar data and Ck3. This class represents a province as Ck3 understands it.
    /// AKA the smallest parts the map is divided into, so baronies, sea provinces, major rivers sections, etc.
    /// </summary>
    /// <param name="waterProvince"></param>
    /// <returns></returns>
    private static Province[] CreateProvinces(Map map)
    {

        throw new NotImplementedException();
        // A provice needs a name, id, color, burg(s), and neighbors, and if it is water or not.




        // var provinces = new Province[provinceCells.Count + waterProvinces.Count];

        // // pId == 0 is the wasteland province.
        // provinces[0] = new Province
        // {
        //     Color = MagickColors.Black,
        //     Name = "x",
        //     Id = 0,
        // };

        // try
        // {
        //     var neighborCellIds = new Dictionary<int, int[]>();
        //     for (int i = 1; i < provinceCells.Count; i++)
        //     {
        //         var color = GetColor(i, provinces.Length);
        //         var province = provinces[i] = provinceCells[i];

        //         province.Color = color;
        //         province.Name = jsonmap.pack.provinces[i].name;
        //         province.Id = jsonmap.pack.provinces[i].i;
        //         province.Burg = jsonmap.pack.burgs[jsonmap.pack.provinces[i].burg];

        //         var cellIds = province.Cells.Select(n => n.Id).ToHashSet();
        //         neighborCellIds[i] = province.Cells.SelectMany(n => n.Neighbors.Where(m => !cellIds.Contains(m))).ToArray();
        //     }

        //     // Create sea provinces
        //     for (int i = 0; i < waterProvinces.Count; i++)
        //     {
        //         var province = provinces[provinceCells.Count + i] = waterProvinces[i];
        //         province.Color = GetColor(provinceCells.Count + i, provinces.Length);
        //         province.Name = "sea";
        //         province.Id = provinceCells.Count + i;
        //         province.IsWater = true;
        //     }

        //     // Populate neighbors
        //     for (int i = 0; i < provinceCells.Count; i++)
        //     {
        //         var neighbors = new HashSet<Province>();

        //         if (neighborCellIds.TryGetValue(i, out var cellIds))
        //         {
        //             var processedNeighbors = new HashSet<int>();
        //             foreach (var cid in cellIds)
        //             {
        //                 if (processedNeighbors.Contains(cid)) continue;

        //                 foreach (var p in provinces.Where(n => n.Id != 0 && !n.IsWater && n.StateId == provinces[i].StateId && n.Cells.Any(m => m.id == cid)))
        //                 {
        //                     neighbors.Add(p);
        //                 }

        //                 processedNeighbors.Add(cid);
        //             }
        //             provinces[i].Neighbors = neighbors.ToArray();
        //         }
        //     }
        // }
        // catch (Exception ex)
        // {
        //     Debugger.Break();
        //     throw;
        // }

        // var finalProvinces = provinces
        //     .Take(1)
        //     .Concat(TransferHangingCells(provinces[1..provinceCells.Count]))
        //     .Concat(provinces[provinceCells.Count..])
        //     .ToArray();

        // return finalProvinces;
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
            await cellsMap.WriteAsync(Helper.GetPath($"{Environment.CurrentDirectory}/cells.png"));
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }

    public static async Task DrawProvincesImage(Map map)
    {
        Console.WriteLine("Drawing provinces image...");
        try
        {
            var settings = new MagickReadSettings()
            {
                Width = Map.MapWidth,
                Height = Map.MapHeight,
            };
            using var cellsMap = new MagickImage("xc:black", settings);

            List<Drawables> drawablesList = new();
            // foreach (var province in map.Provinces.Skip(1))
            // {
            //     if (!province.IsWater) continue;
            //     drawablesList.Add(GenerateCellPolygons(province.Cells, province.Color, map));
            // }
            foreach (var barony in map.Baronies)
            {
                drawablesList.Add(GenerateCellPolygons(barony.Cells, barony.Color, map));
            }

            // Flatten the list of Drawables into a single collection of IDrawable
            IEnumerable<IDrawable> drawables = drawablesList.SelectMany(d => d);

            cellsMap.Draw(drawables);
            var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "provinces.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            Console.WriteLine($"Saving provinces image to '{path}'");
            await cellsMap.WriteAsync(path);
            Console.WriteLine($"Provinces image has been drawn and saved to '{path}'");

            //#if Debugd
            // Open the image
            Console.WriteLine("Debug is on, opening the image...");
            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { path },
                UseShellExecute = true
            };

            Process.Start(psi);
            //#endif


        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }

    public static async Task DrawSingleProvince(Province province, MagickColor color, Map map)
    {
        try
        {

            var settings = new MagickReadSettings()
            {
                Width = Map.MapWidth,
                Height = Map.MapHeight,
            };

            using var provinceMap = new MagickImage("xc:black", settings);
            var drawables = new Drawables();

            foreach (var cell in province.Cells)
            {
                drawables
                    .DisableStrokeAntialias()
                    .StrokeColor(province.Color)
                    .FillColor(color)
                    .Polygon(cell.GeoDataCoordinates.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, Map.MapHeight - (n[1] - map.YOffset) * map.YRatio)));
            }

            provinceMap.Draw(drawables);
            var path = Helper.GetPath(Settings.OutputDirectory, "map_data", $"{province.Name}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await provinceMap.WriteAsync(path);

            //open the image
            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { path },
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }


    }

    private static Drawables GenerateCellPolygons(IEnumerable<Cell> cells, MagickColor color, Map map)
    {
        var drawables = new Drawables();
        foreach (var cell in cells)
        {
            drawables
                .DisableStrokeAntialias()
                .StrokeColor(color)
                .FillColor(color)
                .Polygon(cell.GeoDataCoordinates.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, Map.MapHeight - (n[1] - map.YOffset) * map.YRatio)));
        }
        return drawables;
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

            var landPackCells = map.JsonMap.pack.cells.Where(n => n.biome != 0);
            var landGeoCells = map.GeoMap.features.Select(n => new
            {
                Height = n.properties.height,
                Id = n.properties.id,
                C = n.geometry.coordinates
            }).ToDictionary(n => n.Id, n => n);
            var cells = landPackCells.Select(n =>
            {
                var c = landGeoCells[n.i];
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
            var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await cellsMap.WriteAsync(path);

            //using var file = await Image.LoadAsync(path);
            //file.Mutate(n => n.GaussianBlur(15));
            //file.Save(path);
            using var file = await Image.LoadAsync(path);
            file.Mutate(n => n.GaussianBlur(6));
            file.Save(path);

        }
        catch (Exception ex)
        {

        }
    }

    public static async Task DrawRivers(Map map)
    {
        throw new NotImplementedException();
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
                        .Polygon(cell.GeoDataCoordinates.Select(n => GeoToPixel(n[0], n[1], map)));
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
            var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "rivers.png");
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
        var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "definition.csv");
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
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "building_locators.txt");
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
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "siege_locators.txt");
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
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "combat_locators.txt");
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
                var maxLon = n.Cells.SelectMany(n => n.GeoDataCoordinates).MaxBy(n => n[0])[0];
                var minLon = n.Cells.SelectMany(n => n.GeoDataCoordinates).MinBy(n => n[0])[0];

                var maxLat = n.Cells.SelectMany(n => n.GeoDataCoordinates).MaxBy(n => n[1])[1];
                var minLat = n.Cells.SelectMany(n => n.GeoDataCoordinates).MinBy(n => n[1])[1];

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
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "map_object_data", "player_stack_locators.txt");
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

        var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "default.map");
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
            var path = Helper.GetPath(Settings.OutputDirectory, "common", "province_terrain", "00_province_terrain.txt");
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
            using var cellsMap = new MagickImage(Helper.GetPath(SettingsManager.ExecutablePath, "template_mask.png"));

            var drawables = new Drawables();
            foreach (var cell in cells.Select(n => n.geoDataCoordinates))
            {
                drawables
                    .DisableStrokeAntialias()
                    .StrokeColor(MagickColors.White)
                    .FillColor(MagickColors.White)
                    .Polygon(cell.Select(n => GeoToPixel(n[0], n[1], map)));
            }

            cellsMap.Draw(drawables);

            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", $"{filename}.png");
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
        var path = Helper.GetPath(Settings.OutputDirectory, "common", "defines", "graphic", "00_graphics.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.Copy(Helper.GetPath(SettingsManager.ExecutablePath, "00_graphics.txt"), path, true);
    }
    public static async Task WriteDefines()
    {
        var file = $@"NJominiMap = {{
	WORLD_EXTENTS_X = {Map.MapWidth - 1}
	WORLD_EXTENTS_Y = 51
	WORLD_EXTENTS_Z = {Map.MapHeight - 1}
	WATERLEVEL = 3.8
}}";
        var path = Helper.GetPath(Settings.OutputDirectory, "common", "defines", "00_defines.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file);
    }


    private static async Task<(Dictionary<int, int> baronyCultures, string[] toOriginalCultureName)> GetCultures(Map map)
    {
        var originalCultureNames = await File.ReadAllLinesAsync(Helper.GetPath(SettingsManager.ExecutablePath, "originalCultures.txt"));
        var baronyCultures = map.Empires
            .SelectMany(n => n.kingdoms)
            .SelectMany(n => n.duchies)
            .SelectMany(n => n.counties)
            .SelectMany(n => n.baronies)
            .ToDictionary(n => n.id, n => n.GetProvince().Cells.Select(m => m.culture).Max());

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
            .ToDictionary(n => n.id, n => n.GetProvince().Cells.Select(m => m.religion).Max());

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
                        var str = $@"{map.IdToIndex[n.GetProvince().Id]} = {{
    culture = {n.Culture}
    religion = {n.Religion}
    holding = auto
}}";
                        return str;
                    })
                    .ToArray();

                var file = string.Join('\n', baronies);
                var path = Helper.GetPath(Settings.OutputDirectory, "history", "provinces", $"k_{kingdom.id}.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
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
        var path = Helper.GetPath(Settings.OutputDirectory, "common", "religion", "holy_sites", "00_holy_sites.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file);
    }



    internal static async Task LemurAlgorithm(Map map)
    {
        map.Burgs = RepackBurgsAsDictionary(map);

        map.Cells = RepackCellsFromDataAsDictionary(map);

        map.Cells = RepackCellsAsDictionary(map);
        map.Burgs = AddBurgCellReference(map.Burgs, map.Cells);
        map.Baronies = GenerateBaronies(map.Burgs);
        FormatCheckBurgs(map);
        AssignCellsToBaronies(map);


        // Assigning colurs:
        // Each barony must get a unique colour
        //And each sea province must get a unique colour
        //And then draw every barony and every province to the same file

        AssignColoursToBaroniesAndSeaProvinces(map);


        // Draw the provinces map
        await DrawProvincesImage(map);


        // Exit the program since we are debugging
        Environment.Exit(0);



    }

    private static Dictionary<int, Cell> RepackCellsFromDataAsDictionary(Map map)
    {
        Dictionary<int, Cell> cells = new Dictionary<int, Cell>();

        var cellData = map.GeoMap.features; //Each feature is the representation for a single cell
        //Each cell has a list of properties and a list for geometry
        // The list of properties contains the id, heigh, neighbours e.c.t

        foreach (var cell in cellData)
        {
            cells.Add(cell.properties.id, new Cell()
            {
                Id = cell.properties.id,
                Height = cell.properties.height,
                //Biome = cell.properties.biome, comes form the other json
                //Burg = cell.properties.burg,
                Culture = cell.properties.culture,
                Religion = cell.properties.religion,
                //Province = map.Provinces.First(p => p.Id == cell.properties.province),
                State = cell.properties.state,
                // Neighbour is an array of 
                Neighbors = cell.properties.neighbors,
            });

        }

        Console.WriteLine($"Repacked {cells.Count} cells from data");
        return new Dictionary<int, Cell>();


        // foreach (var cell in map.JsonMap.pack.cells)
        // {
        //     cells.Add(cell.i, new Cell(cell));
        // }
        // return cells;
    }

    private static void FormatCheckBurgs(Map map)
    {
        // Check that every burg lies inside of a province
        // Problem: A burg can be in the 0'th province. This can happoen because not all cells inside a state are assigned a province - as in cases where the state is very small.
        // This means we either have to let the user know that this is a limitation of the program, or we have to generate the missing data.
        // For now I will go with the first option and get back to it later
        try
        {

            foreach (var barony in map.Baronies)
            {
                if (barony.Burg.State == 0)
                {
                    throw new FormatException($"Burg {barony.Name} is in the wasteland");
                }
                if (barony.GetProvince().Id == 0)
                {
                    throw new FormatException($"Burg {barony.Name} is not inside a province. Every burg must be inside a province, even when inside a state");
                }
            }
        }
        catch (FormatException e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine("Program found an unresolvable issue in the Azgaar data model. The program will now exit.");
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
            Environment.Exit(0);
        }
    }

    private static void AssignColoursToBaroniesAndSeaProvinces(Map map)
    {
        //So the base data is all provinces, but then for the land provinces we split it into baronies
        //So we need to assign a colour to each province and each barony, ensuring that there is no overlap between the two
        //We also need to assign a unique colour to each sea province and barony, but we can use GetColor() for that

        //First we'll assign colours to the provinces
        //We'll start by getting all the provinces

        int Ck3_assignedProvinceColorCount = 0;
        //the max number of brovinces and baronies will be the number of provinces that is sea + the number of baronies
        int UniqueColourRange = map.Provinces.Where(p => p.IsWater).Count() + map.Baronies.Count + 10; //+10 to be safe

        var provinces = map.Provinces.Skip(1).ToList(); //Skip the first province since it is always empty (See Azgaar data model)
        foreach (var province in provinces)
        {
            if (!province.IsWater) continue;
            Ck3_assignedProvinceColorCount++;
            if (Settings.Instance.Debug) province.Color = new MagickColor(0, 0, 50);
            else province.Color = GetColor(Ck3_assignedProvinceColorCount, UniqueColourRange);

            Console.WriteLine($"Assigned colour {province.Color} to {province.Name}");
        }
        foreach (var barony in map.Baronies)
        {
            Ck3_assignedProvinceColorCount++;
            barony.Color = GetColor(Ck3_assignedProvinceColorCount, UniqueColourRange);
            Console.WriteLine($"Assigned colour {barony.Color} to {barony.Name}");
        }
        //pringt the number of colours assigned
        Console.WriteLine($"Assigned {Ck3_assignedProvinceColorCount} colours to provinces and baronies");

    }

    /// <summary>
    /// Assign cells to baronies based on distance to burg
    /// </summary>
    /// <param name="map"></param>
    private static async void AssignCellsToBaronies(Map map)
    {


        foreach (var province in map.Provinces)
        {
            if (province.IsWater)
            {
                continue;
            }

            // We'll start by getting all the baronies in the province
            var baroniesInProvince = map.Baronies.Where(b => b.GetProvince() == province).ToList();
            if (baroniesInProvince.Count == 0)
            {
                Console.WriteLine($"No baronies in {province.Name}");
                continue;
            }

            //Sort them on population size decending baroniesInProvince[0].Burg.population
            baroniesInProvince.Sort((a, b) => b.Burg.Population.CompareTo(a.Burg.Population));

            // Now get all cells in the province that are not already assigned to a burg (country side cells)
            var CountrysideCells = province.Cells.Where(c => !c.hasBurg).ToDictionary(c => c.id, c => c);

            // Have each barony grow outwards until all countryside cells are assigned among the burgs based on distance
            while (CountrysideCells.Count > 0)
            {
                bool noMoreRoom = true;
                foreach (Barony barony in baroniesInProvince)
                {
                    //Look at it's assigned cells
                    // And generate a list of neighbouring cells from the list of countryside cells
                    var neighbors = barony.Cells.SelectMany(c => c.neighbors).Where(c => CountrysideCells.ContainsKey(c)).Select(c => CountrysideCells[c]).ToList();
                    if (neighbors.Count == 0)
                    {
                        continue;
                    }
                    noMoreRoom = false;
                    //Then sort them by distance to the burg
                    neighbors.Sort((a, b) => a.DistanceSquared(barony.Burg.Cell).CompareTo(b.DistanceSquared(barony.Burg.Cell)));
                    //Then assign the closest cell to the burg
                    barony.Cells.Add(neighbors[0]);
                    //And remove it from the list of countryside cells
                    CountrysideCells.Remove(neighbors[0].id);
                }
                if (noMoreRoom)
                {
                    Console.WriteLine("No more room in any barony");
                    break;
                }
            }

        }
    }

    private static Dictionary<int, ConverterBurg> AddBurgCellReference(Dictionary<int, ConverterBurg> burgs, Dictionary<int, Cell> cells)
    {
        //For each burg (skip 0'eth) find the cell it is referenceing by cell id and assign it to the burg
        foreach (var burg in burgs.Skip(1))
        {
            burg.Value.Cell = cells[burg.Value.azgaarBurg.cell];
        }

        return burgs;
    }

    /// <summary>
    /// Generate a list of baronies with the bare minimum of information
    /// </summary>
    /// <param name="burgs">Burgs to base the baronies on</param>
    /// <returns>A list of baronies</returns>
    private static List<Barony> GenerateBaronies(Dictionary<int, ConverterBurg> burgs)
    {
        // Next we instanciate a list of baronies. Since we know the final size of the list we can set the capacity to the number of burgs
        List<Barony> baronies = new(burgs.Count - 1);
        foreach (var burg in burgs.Skip(1)) //0'eth entry is always empty (See Azgaar data model)
        {
            baronies.Add(new Barony(burg.Value));
        }

        return baronies;
    }

    private static Dictionary<int, ConverterBurg> RepackBurgsAsDictionary(Map map)
    {
        // Repack the burgs as a dictionary with the burg.id as the key for easy lookup
        return map.JsonMap.pack.burgs.ToDictionary(
            burg => burg.i,
            burg => new ConverterBurg(burg));
    }

    private static Dictionary<int, Cell> RepackCellsAsDictionary(Map map)
    {
        // We use a dictionary because then it is easy to look up a cell by its id, which is what we'll most likley need to do most of the time.
        var Cells = map.Provinces
            .SelectMany(n => n.Cells) // Flatten the list
            .Distinct(new CellComparer()) // Remove duplicates
            .ToDictionary(c => c.id); // Convert to dictionary with cell id as the key
        
        //for logging puproses, get the total number of cells among all provinces
        int totalCells = map.Provinces.SelectMany(n => n.Cells).Count();

        //get a lisrt of iotems that have been removed


        
        Console.WriteLine($"Repacked {Cells.Count} cells as a dictionary. Removed {totalCells - Cells.Count} duplicates");

        // Mark all the cells that have a burg
        Console.WriteLine("Marking cells with burgs");
        foreach (var burg in map.JsonMap.pack.burgs.Skip(1)) //Skip the 0'eth entry since it is always empty (See Azgaar data model)
        {
            if (burg.cell == 590)
            {
                Console.WriteLine("Found 590");
                try
                {
                    //print every cell in the cell list
                    foreach (var cell in Cells)
                    {
                        Console.WriteLine(cell.Key);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

            }
            Cells[burg.cell].Burg = burg;
        }
        return Cells;
    }

    class CellComparer : IEqualityComparer<Cell>
    {
        public bool Equals(Cell x, Cell y)
        {
            // Compare whether the IDs are equal
            return x.id == y.id;
        }

        public int GetHashCode(Cell cell)
        {
            if (Object.ReferenceEquals(cell, null)) return 0;

            // Use ID hash code for the hash code
            return cell.id.GetHashCode();
        }
    }
}
