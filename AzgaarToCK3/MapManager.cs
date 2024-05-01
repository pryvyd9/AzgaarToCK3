using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzgaarToCK3;

public static class MapManager
{
    private const int MapWidth = 8192;
    private const int MapHeight = 4096;
    private const int WaterLevelHeight = 30;

    private const bool ShouldCreateFolderStructure = true;

    #region Magic

    //private static readonly double canvasSizeX = 1832;
    //private static readonly double canvasSizeY = 999;
    private static readonly double canvasSizeX = 8192;
    private static readonly double canvasSizeY = 4096;
    private static readonly double pixelXRatio = MapWidth / canvasSizeX;
    private static readonly double pixelYRatio = MapHeight / canvasSizeY;
    // magic numbers
    //private const double pixelErrorRatio = 1.1;
    //private const int pixelErrorOffset = -825;

    private const double pixelErrorRatio = 1.12;
    private const int pixelErrorOffset = -600;

    #endregion

    public static async Task<GeoMap> LoadGeojson()
    {
        try
        {
            var file = await File.ReadAllTextAsync("input.geojson");
            var geomap = JsonSerializer.Deserialize<GeoMap>(file);
            return geomap;
        }
        catch(Exception e)
        {
            Debugger.Break();
            return null;
        }
      
    }
    public static async Task<GeoMapRivers> LoadGeojsonRivers()
    {
        try
        {
            var file = await File.ReadAllTextAsync("inputRivers.geojson");
            var geomap = JsonSerializer.Deserialize<GeoMapRivers>(file);
            return geomap;
        }
        catch (Exception e)
        {
            Debugger.Break();
            return null;
        }

    }

    public static async Task<JsonMap> LoadJson()
    {
        try
        {
            var file = await File.ReadAllTextAsync("input.json");

            var jsonmap = JsonSerializer.Deserialize<JsonMap>(file);
            return jsonmap;
        }
        catch (Exception e)
        {
            Debugger.Break();
            return null;
        }

    }

    private static MagickColor GetColor(int i, int maxI)
    {
        // max 24bit color
        var maxColor = 256 * 256 * 256;

        var color = maxColor / maxI * i;

        byte r = (byte)((color & 0x0000FF) >> 0);
        byte g = (byte)((color & 0x00FF00) >> 8);
        byte b = (byte)((color & 0xFF0000) >> 16);

        var c = new MagickColor(r, g, b);
        
        // prevent system colors
        //if (c == new MagickColor("#5C5D0B"))
        //{
        //    c.B++;
        //}

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
    private static void TransferHangingCells(Province[] nonWaterProvinces)
    {
        try
        {
            // province to where to transfer to. What to transfer.
            var cellsToTransfer = new Dictionary<Province, Cell>();

            // Find cells that don't touch the province but still belong to it.
            // Reassign it to the neighbor province.
            foreach (var province in nonWaterProvinces)
            {
                var cells = province.Cells;
                if (province.Color.ToString() == "#9A4FE7FF")
                {

                }

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
                            // Is an island. Let it stay as part of province.
                            //Debugger.Break();
                            continue;
                        }

                        cellsToTransfer[nonWaterNeighborProvince] = cell;
                        cellsToRemove.Add(cell);
                    }
                }

                cellsToRemove.ForEach(n => cells.Remove(n));
            }

            // Transfer cells
            foreach (var (p, c) in cellsToTransfer)
            {
                p.Cells.Add(c);
            }
        }
        catch (Exception ex)
        {
            Debugger.Break();
        }
    }
    private static List<Province> CreateWaterProvinces(Province waterProvince)
    {
        try
        {
            var cells = waterProvince.Cells;

            var areaPerProvince = 150000;

            var unprocessedCells = waterProvince.Cells.ToDictionary(n => n.id, n => n);
            var provinces = new List<Province>();

            do
            {
                // If empty then the loop will break anyways.
                var currentCell = unprocessedCells.Values.FirstOrDefault();

                var currentArea = 0;
                var accumulatedNeighbors = new HashSet<int>();

                for (int i = 0; currentArea < areaPerProvince; i++)
                {
                    if (i == 0)
                    {
                        provinces.Add(new Province());
                    }

                    unprocessedCells.Remove(currentCell.id);
                    provinces.Last().Cells.Add(currentCell);
                    currentArea += currentCell.area;

                    foreach (var n in currentCell.neighbors)
                    {
                        accumulatedNeighbors.Add(n);
                    }

                    if (accumulatedNeighbors.Select(n => unprocessedCells.GetValueOrDefault(n)).FirstOrDefault(n => n is not null) is { } neighbor)
                    {
                        currentCell = neighbor;
                        accumulatedNeighbors.Remove(neighbor.id);
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
        }

        TransferHangingCells(provinces[1..provinceCells.Count]);

        return provinces;
    }


#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public static async Task<Map> ConvertMap(GeoMap geoMap, GeoMapRivers geoMapRivers, JsonMap jsonMap)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        var flatCoordinates = geoMap!.features.SelectMany(n => n.geometry.coordinates).SelectMany(n => n);

        // Cells
        var maxX = flatCoordinates.MaxBy(n => n[0])![0];
        var maxY = flatCoordinates.MaxBy(n => n[1])![1];

        var minX = flatCoordinates.MinBy(n => n[0])![0];
        var minY = flatCoordinates.MinBy(n => n[1])![1];

        var xRatio = MapWidth / (maxX - minX);
        var yRatio = MapHeight / (maxY - minY);

        var map = new Map
        {
            GeoMap = geoMap,
            Rivers = geoMapRivers,
            JsonMap = jsonMap,
            XOffset= minX,
            YOffset= minY,
            XRatio = xRatio,
            YRatio = yRatio,
            Provinces = CreateProvinces(geoMap, jsonMap),
        };

        return map;
    }

    public static async Task DrawCells(Map map)
    {
        try
        {
            var settings = new MagickReadSettings()
            {
                Width = MapWidth,
                Height = MapHeight,
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
                        .Polygon(cell.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)));
                }
            }

            cellsMap.Draw(drawables);
            await cellsMap.WriteAsync($"{Environment.CurrentDirectory}/cells.png");

        }
        catch (Exception ex)
        {

        }
      

    }
    public static async Task DrawProvinces(Map map)
    {
        try
        {
            var settings = new MagickReadSettings()
            {
                Width = MapWidth,
                Height = MapHeight,
            };
            //using var cellsMap = new MagickImage("xc:#5C5D0B", settings);
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
                        .Polygon(cell.cells.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)));
                }
            }
          
            cellsMap.Draw(drawables);
            var path = ShouldCreateFolderStructure
                ? $"{Environment.CurrentDirectory}/mod/map_data/provinces.png"
                : $"{Environment.CurrentDirectory}/provinces.png";

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            await cellsMap.WriteAsync(path);
        }
        catch (Exception ex)
        {
            Debugger.Break();
        }


    }
    public static async Task DrawHeightMap(Map map)
    {
        try
        {
            var maxHeight = map.Provinces.Skip(1).SelectMany(n => n.Cells).MaxBy(n => n.height)!.height;
            //var minHeight = map.Provinces.Skip(1).SelectMany(n => n.Cells).MinBy(n => n.height)!.height;

            var settings = new MagickReadSettings()
            {
                Width = MapWidth,
                Height = MapHeight,
            };
            using var cellsMap = new MagickImage("xc:black", settings);

            var drawables = new Drawables();
            foreach (var province in map.Provinces.Skip(1).Where(n => !n.IsWater))
            {
                foreach (var cell in province.Cells)
                {
                    var trimmedHeight = cell.height * (255 - WaterLevelHeight) / maxHeight + WaterLevelHeight;
                    var culledHeight = (byte)trimmedHeight;

                    var color = new MagickColor(culledHeight, culledHeight, culledHeight);
                    drawables
                        .DisableStrokeAntialias()
                        .StrokeColor(color)
                        .FillColor(color)
                        .Polygon(cell.cells.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)));
                }
            }

            cellsMap.Draw(drawables);
            var path = ShouldCreateFolderStructure
                ? $"{Environment.CurrentDirectory}/mod/map_data/heightmap.png"
                : $"{Environment.CurrentDirectory}/heightmap.png";
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
                Width = MapWidth,
                Height = MapHeight,
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
                        .Polygon(cell.cells.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)));
                }
            }

            foreach (var river in map.Rivers.features)
            {
                drawables
                    .DisableStrokeAntialias()
                    .StrokeColor(new MagickColor("#00E1FF"))
                    .StrokeWidth(0.5)
                    .Polyline(river.geometry.coordinates.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)));
            }

            cellsMap.Draw(drawables);
            var path = ShouldCreateFolderStructure
                ? $"{Environment.CurrentDirectory}/mod/map_data/rivers.png"
                : $"{Environment.CurrentDirectory}/rivers.png";
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
        }
    }
    public static async Task WriteDefinition(Map map)
    {
        var lines = map.Provinces.Select((n, i) => $"{i};{n.Color.R};{n.Color.G};{n.Color.B};{n.Name};x;");
        var path = ShouldCreateFolderStructure
              ? $"{Environment.CurrentDirectory}/mod/map_data/definition.csv"
              : $"{Environment.CurrentDirectory}/definition.csv";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllLinesAsync(path, lines);
    }
    public static async Task WriteBuildingLocators(Map map)
    {
        //var minX = map.Provinces.Where(n => n.Burg is not null).Select(n => n.Burg).MinBy()
        var lines = map.Provinces.Where(n => n.Burg is not null).Select(n =>
        {
            var x = n.Burg.x * pixelErrorRatio * pixelXRatio + pixelErrorOffset;
            var y = MapHeight - n.Burg.y * pixelYRatio;
            var str =
$@"        {{
            id = {n.Id}
            position ={{ {x:0.000000} {0f:0.000000} {y:0.000000} }}
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
            var path = ShouldCreateFolderStructure
                ? $"{Environment.CurrentDirectory}/mod/gfx/map/map_object_data/building_locators.txt"
                : $"{Environment.CurrentDirectory}/building_locators.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            await File.WriteAllTextAsync(path, file);
        }
        catch (Exception e)
        {
            Debugger.Break();
        }
      
    }
    public static async Task WriteSiegeLocators(Map map)
    {
        var offset = new PointD(20, 0);
        var lines = map.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var str =
$@"        {{
            id = {n.Id}
            position ={{ {n.Burg.x * pixelErrorRatio * pixelXRatio + offset.X + pixelErrorOffset:0.000000} {0f:0.000000} {MapHeight - n.Burg.y * pixelYRatio + offset.Y:0.000000} }}
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
            var path = ShouldCreateFolderStructure
                ? $"{Environment.CurrentDirectory}/mod/gfx/map/map_object_data/siege_locators.txt"
                : $"{Environment.CurrentDirectory}/siege_locators.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await File.WriteAllTextAsync(path, file);
        }
        catch (Exception e)
        {
            Debugger.Break();
        }
    }
    public static async Task WriteCombatLocators(Map map)
    {
        var offset = new PointD(0, 20);
        var lines = map.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var str =
$@"        {{
            id = {n.Id}
            position ={{ {n.Burg.x * pixelErrorRatio * pixelXRatio + offset.X + pixelErrorOffset:0.000000} {0f:0.000000} {MapHeight - n.Burg.y * pixelYRatio + offset.Y:0.000000} }}
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
            var path = ShouldCreateFolderStructure
                ? $"{Environment.CurrentDirectory}/mod/gfx/map/map_object_data/combat_locators.txt"
                : $"{Environment.CurrentDirectory}/combat_locators.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await File.WriteAllTextAsync(path, file);
        }
        catch (Exception e)
        {
            Debugger.Break();
        }
    }
    public static async Task WritePlayerStackLocators(Map map)
    {
        var offset = new PointD(20, 20);
        var lines = map.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var x = n.Burg.x * pixelErrorRatio * pixelXRatio + offset.X + pixelErrorOffset;
            var y = MapHeight - n.Burg.y * pixelYRatio + offset.Y;
            var str =
$@"        {{
            id = {n.Id}
            position ={{ {x:0.000000} {0f:0.000000} {y:0.000000} }}
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
            var path = ShouldCreateFolderStructure
                ? $"{Environment.CurrentDirectory}/mod/gfx/map/map_object_data/player_stack_locators.txt"
                : $"{Environment.CurrentDirectory}/player_stack_locators.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await File.WriteAllTextAsync(path, file);
        }
        catch (Exception e)
        {
            Debugger.Break();
        }
    }

    private static List<Duchy> CreateDuchies(Map map)
    {
        try
        {
            var duchies = new List<Duchy>();
            var processedProvinces = new HashSet<Province>();

            foreach (var state in map.JsonMap.pack.states.Where(n => n.provinces.Any()))
            {
                var provinces = state.provinces.Select(n => map.Provinces.First(m => m.Id == n)).ToArray();
              
                // Each county should have 4 or fewer counties.
                var countyCount = state.provinces.Length / 4;
                var unprocessedProvinces = provinces.Except(processedProvinces).ToHashSet();
                var counties = new List<County>();
                var accumulatedProvinces = new List<Province>();

                var currentProvince = provinces[0];
              
                do
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (processedProvinces.Contains(currentProvince))
                        {
                            break;
                        }
                        if (i == 0)
                        {
                            counties.Add(new County()
                            {
                                Color = currentProvince.Color,
                                CapitalName = currentProvince.Name,
                                Name = "Country of " + currentProvince.Name,
                            });
                        }

                        unprocessedProvinces.Remove(currentProvince);
                        processedProvinces.Add(currentProvince);
                        accumulatedProvinces.Add(currentProvince);
                     
                        counties.Last().baronies.Add(new Barony(currentProvince, currentProvince.Name, currentProvince.Color));

                        if (currentProvince.Neighbors.FirstOrDefault(n => !processedProvinces.Contains(n)) is { } neighbor)
                        {
                            currentProvince = neighbor;
                        }
                        else
                        {
                            break;
                        }
                    }

                    // If empty then the loop will break anyways.
                    currentProvince = unprocessedProvinces.FirstOrDefault();
                } while (unprocessedProvinces.Count > 0);

              
                duchies.Add(new Duchy(counties.ToArray(), "Duchy of " + state.name, counties.First().Color, counties.First().CapitalName));
            }
            return duchies;
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }
    public static Empire[] CreateTitles(Map map)
    {
        try
        {
            var duchies = CreateDuchies(map);
            //var cultures = map.JsonMap.pack.cultures;

            var duchyCultures = new Dictionary<int, List<Duchy>>();
            foreach (var duchy in duchies)
            {
                var primaryDuchyCultureId = duchy.counties
                    .SelectMany(n => n.baronies)
                    .SelectMany(n => n.province.Cells)
                    .Select(n => n.culture)
                    .GroupBy(n => n)
                    .ToDictionary(n => n.Key, n => n.Count())
                    .MaxBy(n => n.Value)
                    .Key;

                if (!duchyCultures.ContainsKey(primaryDuchyCultureId))
                {
                    duchyCultures[primaryDuchyCultureId] = new List<Duchy>();
                }

                duchyCultures[primaryDuchyCultureId].Add(duchy);
            }

            var kingdoms = duchyCultures.Select(n =>
            {
                var cultureId = n.Key;
                var duchies = n.Value;
                return new Kingdom(
                    duchies.ToArray(),
                    duchies.Count > 1,
                    "Kingdom of " + map.JsonMap.pack.cultures.First(n => n.i == cultureId).name,
                    duchies.First().color,
                    duchies.First().capitalName);
            }).ToArray();

            var kingdomReligions = new Dictionary<int, List<Kingdom>>();
            foreach (var kingdom in kingdoms)
            {
                var primaryDuchyReligionId = kingdom.duchies
                    .SelectMany(n => n.counties)
                    .SelectMany(n => n.baronies)
                    .SelectMany(n => n.province.Cells)
                    .Select(n => n.religion)
                    .GroupBy(n => n)
                    .ToDictionary(n => n.Key, n => n.Count())
                    .MaxBy(n => n.Value)
                    .Key;

                if (!kingdomReligions.ContainsKey(primaryDuchyReligionId))
                {
                    kingdomReligions[primaryDuchyReligionId] = new List<Kingdom>();
                }

                kingdomReligions[primaryDuchyReligionId].Add(kingdom);
            }

            var empires = kingdomReligions.Select(n =>
            {
                var religionId = n.Key;
                var kingdoms = n.Value;
                return new Empire(
                    kingdoms.ToArray(),
                    kingdoms.Count > 1,
                    "Empire of " + map.JsonMap.pack.religions.First(n => n.i == religionId).name,
                    kingdoms.First().color,
                    kingdoms.First().capitalName);
            }).ToArray();

            return empires;
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
      
    }
    public static async Task WriteLandedTitles(Empire[] empires)
    {
        int ei = 0;
        int ki = 0;
        int di = 0;
        int ci = 0;
        int bi = 0;

        string[] GetBaronies(Barony[] baronies)
        {
            return baronies.Select((n, i) =>
            {
                return $@"                b_{bi++} = {{
                    color = {{ {n.color.R} {n.color.G} {n.color.B} }}
                    color2 = {{ 255 255 255 }}
                    province = {n.province.Id}
                }}";
            }).ToArray();
        }

        string[] GetCounties(County[] counties)
        {
            return counties.Select((n, i) => $@"            c_{ci++} = {{
                color = {{ {n.Color.R} {n.Color.G} {n.Color.B} }}
                color2 = {{ 255 255 255 }}
{string.Join("\n", GetBaronies(n.baronies.ToArray()))}
            }}").ToArray();
        }

        string[] GetDuchies(Duchy[] duchies)
        {
            return duchies.Select((d, i) => $@"        d_{di++} = {{
            color = {{ {d.color.R} {d.color.G} {d.color.B} }}
            color2 = {{ 255 255 255 }}
            capital = c_{ci}
{string.Join("\n", GetCounties(d.counties))}
        }}").ToArray();
        }

        string[] GetKingdoms(Kingdom[] kingdoms)
        {
            return kingdoms.Select((k, i) => $@"    k_{ki++} = {{
        color = {{ {k.color.R} {k.color.G} {k.color.B} }}
        color2 = {{ 255 255 255 }}
        capital = c_{ci}
        {(k.isAllowed ? "" : "allow = { always = no }")}
{string.Join("\n", GetDuchies(k.duchies))}
    }}").ToArray();
        }

        string[] GetEmpires()
        {
            return empires.Select((e, i) => $@"e_{ei++} = {{
    color = {{ {e.color.R} {e.color.G} {e.color.B} }}
    color2 = {{ 255 255 255 }}
    capital = c_{ci}
    definite_form = yes
    {(e.isAllowed ? "" : "allow = { always = no }")}
{string.Join("\n", GetKingdoms(e.kingdoms))}
}}").ToArray();
        }

        var file = $@"@correct_culture_primary_score = 100
@better_than_the_alternatives_score = 50
@always_primary_score = 1000
{string.Join("\n", GetEmpires())}
# These titles cut hundreds of errors from logs. 
e_hre = {{ landless = yes }}
e_byzantium = {{ landless = yes }}
e_roman_empire = {{ landless = yes }}";

        var path = ShouldCreateFolderStructure
            ? $"{Environment.CurrentDirectory}/mod/common/landed_titles/00_landed_titles.txt"
            : $"{Environment.CurrentDirectory}/00_landed_titles.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file);
    }
    public static async Task WriteTitleLocalization(Empire[] empires)
    {
        int ei = 0;
        int ki = 0;
        int di = 0;
        int ci = 0;
        int bi = 0;

        var lines = new List<string>();

        foreach (var e in empires)
        {
            lines.Add($"e_{ei++}: \"{e.name}\"");
            foreach (var k in e.kingdoms)
            {
                lines.Add($"k_{ki++}: \"{k.name}\"");
                foreach (var d in k.duchies)
                {
                    lines.Add($"d_{di++}: \"{d.name}\"");
                    foreach (var c in d.counties)
                    {
                        lines.Add($"c_{ci++}: \"{c.Name}\"");
                        foreach (var b in c.baronies)
                        {
                            lines.Add($"b_{bi++}: \"{b.name}\"");
                        }
                    }
                }
            }
        }

        var file = $@"l_english:
 TITLE_NAME:0 ""$NAME$""
 TITLE_TIERED_NAME:0 ""$TIER|U$ of $NAME$""
 TITLE_CLAN_TIERED_NAME:0 ""the $NAME$ $TIER|U$""
 TITLE_CLAN_TIERED_WITH_UNDERLYING_NAME:0 ""the $NAME$ $TIER|U$ #F ($TIER|U$ of $BASE_NAME$) #!""
 TITLE_TIER_AS_NAME:0 ""$TIER|U$""

 {string.Join("\n ", lines)}";

        var path = ShouldCreateFolderStructure
            ? $"{Environment.CurrentDirectory}/mod/localization/english/titles_l_english.yml"
            : $"{Environment.CurrentDirectory}/titles_l_english.yml";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file, new UTF8Encoding(true));
    }
    public static async Task WriteDefault(Map map)
    {
        var waterProvinces = map.Provinces.Where(n => n.IsWater).Select(n => n.Id);
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

#North European Seas
sea_zones = LIST {{ {string.Join(" ", waterProvinces)} }} #French & Iberian atlantic coasts

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

        var path = ShouldCreateFolderStructure
             ? $"{Environment.CurrentDirectory}/mod/map_data/default.map"
             : $"{Environment.CurrentDirectory}/default.map";
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        await File.WriteAllTextAsync(path, file);
    }

    // Biomes
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
            var path = ShouldCreateFolderStructure

         ? $"{Environment.CurrentDirectory}/mod/common/province_terrain/00_province_terrain.txt"
         : $"{Environment.CurrentDirectory}/00_province_terrain.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            await File.WriteAllTextAsync(path, file);
        }
        catch (Exception ex)
        {
            Debugger.Break();
        }
       
    }
    //private static async Task WriteMask(float[][][] cells, Map map, string filename)
    //{
    //    try
    //    {
    //        using var cellsMap = new MagickImage("template_mask.png");

    //        var drawables = new Drawables();
    //        foreach (var cell in cells)
    //        {
    //            drawables
    //                .DisableStrokeAntialias()
    //                .StrokeColor(MagickColors.White)
    //                .FillColor(MagickColors.White)
    //                .Polygon(cell.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)));
    //        }

    //        cellsMap.Draw(drawables);
    //        var path = ShouldCreateFolderStructure
    //            ? $"{Environment.CurrentDirectory}/mod/gfx/map/terrain/{filename}.png"
    //            : $"{Environment.CurrentDirectory}/{filename}.png";

    //        Directory.CreateDirectory(Path.GetDirectoryName(path));

    //        await cellsMap.WriteAsync(path, MagickFormat.Png00);

    //        //using var file = await Image.LoadAsync(path);
    //        //file.Mutate(n => n.GaussianBlur(15));
    //        //file.Save(path);
    //    }
    //    catch (Exception ex)
    //    {
    //        Debugger.Break();
    //    }
    //}
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
                    .Polygon(cell.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)));
            }

            cellsMap.Draw(drawables);
            var path = ShouldCreateFolderStructure
                ? $"{Environment.CurrentDirectory}/mod/gfx/map/terrain/{filename}.png"
                : $"{Environment.CurrentDirectory}/{filename}.png";

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            await cellsMap.WriteAsync(path, MagickFormat.Png00);

            //using var file = await Image.LoadAsync(path);
            //file.Mutate(n => n.GaussianBlur(15));
            //file.Save(path);
        }
        catch (Exception ex)
        {
            Debugger.Break();
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

        // hills
        {
            var cells = nonWaterProvinceCells
                 .Where(n => Helper.IsCellHills(n.biome, n.height));

            await WriteMask(cells, map, "hills_01_mask");
        }
        // mountains
        {
            var cells = nonWaterProvinceCells
                .Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellMountains(n.height));

            await WriteMask(cells, map, "mountain_02_mask");
        }
        // HighMountains
        {
            var cells = nonWaterProvinceCells
               .Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellHighMountains(n.height));

            await WriteMask(cells, map, "mountain_02_snow_mask");
        }
        // plains
        {
            var cells = provinceBiomes
                .Where(n => n.Biome == "plains")
                .SelectMany(n => n.Province.Cells);

            await WriteMask(cells, map, "plains_01_mask");
        }
        // farmlands
        {
            var cells = nonWaterProvinceCells
               .Where(n => Helper.MapBiome(n.biome) == "farmlands");

            await WriteMask(cells, map, "farmland_01_mask");
        }
        // Desert
        {
            var cells = nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "desert");

            await WriteMask(cells, map, "desert_01_mask");
        }
        // desert_mountains
        {
            var cells = nonWaterProvinceCells
                .Where(n => Helper.MapBiome(n.biome) is "desert" && Helper.IsCellMountains(n.height));

            await WriteMask(cells, map, "mountain_02_desert_mask");
        }
        // oasis
        {
            var cells = nonWaterProvinceCells
                .Where(n => Helper.MapBiome(n.biome) == "oasis");

            await WriteMask(cells, map, "oasis_mask");
        }
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
        // taiga
        {
            var cells = nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "taiga");

            await WriteMask(cells, map, "forest_pine_01_mask");
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
        // drylands
        {
            var cells = nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "drylands");

            await WriteMask(cells, map, "drylands_01_mask");
        }
    }
}
