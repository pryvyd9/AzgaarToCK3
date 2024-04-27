using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AzgaarToCK3;

public static class MapManager
{
    private const int MapWidth = 8192;
    private const int MapHeight = 4096;
    private const int WaterLevelHeight = 30;


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

        return new MagickColor(r, g, b);
    }

    private static Dictionary<int, Province> GetProvinceCells(GeoMap geomap)
    {
        var provinces = new Dictionary<int, Province>();
        foreach (var feature in geomap.features)
        {
            var provinceId = feature.properties.province;

            if (!provinces.ContainsKey(provinceId))
            {
                provinces[provinceId] = new Province();
            }

            var cells = feature.geometry.coordinates.Select(n => new Cell(feature.properties.id, feature.properties.height, n, feature.properties.neighbors));
            provinces[provinceId].Cells.AddRange(cells);
        }
       
        return provinces;
    }

    private static void TransferHangingCells(Province[] provinces)
    {
        try
        {
            // province to where to transfer to. What to transfer.
            var cellsToTransfer = new Dictionary<Province, Cell>();

            // Find cells that don't touch the province but still belong to it.
            // Reassign it to the neighbor province.
            foreach (var province in provinces.Skip(1))
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
                        var nonWaterNeighborProvince = provinces.Skip(1).FirstOrDefault(p =>
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

    private static Province[] CreateProvinces(GeoMap geomap, JsonMap jsonmap)
    {
        var provinceCells = GetProvinceCells(geomap);
        var provinces = new Province[provinceCells.Count];

        // pId == 0 is ocean.
        provinces[0] = provinceCells[0];
        provinces[0].Color = MagickColors.Black;
        provinces[0].Name = "x";
        provinces[0].Id = 0;

        int i = 1;
        try
        {
            for (; i < provinces.Length; i++)
            {
                var color = GetColor(i, provinces.Length);
                provinces[i] = provinceCells[i];
                provinces[i].Color = color;
                provinces[i].Name = jsonmap.pack.provinces[i].name;
                provinces[i].Id = jsonmap.pack.provinces[i].i;
                provinces[i].Burg = jsonmap.pack.burgs.FirstOrDefault(n => provinces[i].Cells.Any(m => m.id == n.cell));

                if (provinces[i].Burg is null)
                {

                }
            }
        }
        catch(Exception ex)
        {
            Debugger.Break();
        }

        TransferHangingCells(provinces);

        return provinces;
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public static async Task<Map> ConvertMap(GeoMap geoMap, JsonMap jsonMap)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        var flatCoordinates = geoMap!.features.SelectMany(n => n.geometry.coordinates).SelectMany(n => n);

        // Cells
        var maxX = flatCoordinates.MaxBy(n => n[0])![0];
        var maxY = flatCoordinates.MaxBy(n => n[1])![1];

        var minX = flatCoordinates.MinBy(n => n[0])![0];
        var minY = flatCoordinates.MinBy(n => n[1])![1];

        var xRatio = MapWidth / (Math.Abs(minX) + Math.Abs(maxX));
        var yRatio = MapHeight / (Math.Abs(minY) + Math.Abs(maxY));

        var map = new Map
        {
            GeoMap = geoMap,
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
            using var cellsMap = new MagickImage("xc:#5C5D0B", settings);

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
            // Draw cells for debugging
            //var featuresToTest = map.GeoMap.features.Skip(786).Take(2);

            //foreach (var feature in featuresToTest)
            //{
            //    foreach (var cell in feature.geometry.coordinates)
            //    {
            //        drawables
            //            .DisableStrokeAntialias()
            //            .StrokeWidth(2)
            //            .StrokeColor(MagickColors.Black)
            //            .FillOpacity(new Percentage(0))
            //            .Polygon(cell.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)));
            //    }
            //}

            cellsMap.Draw(drawables);
            await cellsMap.WriteAsync($"{Environment.CurrentDirectory}/provinces.png");

        }
        catch (Exception ex)
        {

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
            foreach (var province in map.Provinces.Skip(1))
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
            var outName = $"{Environment.CurrentDirectory}/heightmap.png";
            await cellsMap.WriteAsync(outName);

            using var file = await SixLabors.ImageSharp.Image.LoadAsync(outName);
            file.Mutate(n => n.GaussianBlur(15));
            file.Save(outName);

        }
        catch (Exception ex)
        {

        }
    }
    public static async Task WriteDefinition(Map map)
    {
        var lines = map.Provinces.Select((n, i) => $"{i};{n.Color.R};{n.Color.G};{n.Color.B};{n.Name};x;");
        await File.WriteAllLinesAsync("definition.csv", lines);
    }
    public static async Task WriteBuildingLocators(Map map)
    {
        var canvasSizeX = 1832;
        var canvasSizeY = 999;
        var xRatio = MapWidth / canvasSizeX;
        var yRatio = MapHeight / canvasSizeY;

        //var minX = map.Provinces.Where(n => n.Burg is not null).Select(n => n.Burg).MinBy()
        var lines = map.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var str =
$@"        {{
            id = {i}
            position ={{ {n.Burg.x * xRatio:0.00000} {0f:0.00000} {MapHeight - n.Burg.y * yRatio:0.00000} }}
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

            await File.WriteAllTextAsync("building_locators.txt", file);
        }
        catch (Exception e)
        {
            Debugger.Break();
        }
      
    }
    public static async Task WriteSiegeLocators(Map map)
    {
        var canvasSizeX = 1832;
        var canvasSizeY = 999;
        var xRatio = MapWidth / canvasSizeX;
        var yRatio = MapHeight / canvasSizeY;

        var offset = new PointD(10, 0);
        var lines = map.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var str =
$@"        {{
            id = {i}
            position ={{ {n.Burg.x * xRatio + offset.X:0.00000} {0f:0.00000} {MapHeight - n.Burg.y * yRatio + offset.Y:0.00000} }}
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
            await File.WriteAllTextAsync("siege_locators.txt", file);
        }
        catch (Exception e)
        {
            Debugger.Break();
        }
    }
    public static async Task WriteCombatLocators(Map map)
    {
        var canvasSizeX = 1832;
        var canvasSizeY = 999;
        var xRatio = MapWidth / canvasSizeX;
        var yRatio = MapHeight / canvasSizeY;
        var offset = new PointD(0, 100);
        var lines = map.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var str =
$@"        {{
            id = {i}
            position ={{ {n.Burg.x * xRatio + offset.X:0.00000} {0f:0.00000} {MapHeight - n.Burg.y * yRatio + offset.Y:0.00000} }}
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
            await File.WriteAllTextAsync("combat_locators.txt", file);
        }
        catch (Exception e)
        {
            Debugger.Break();
        }
    }
    public static async Task WritePlayerStackLocators(Map map)
    {
        var canvasSizeX = 1832;
        var canvasSizeY = 999;
        var xRatio = MapWidth / canvasSizeX;
        var yRatio = MapHeight / canvasSizeY;
        var offset = new PointD(100, 100);
        var lines = map.Provinces.Where(n => n.Burg is not null).Select((n, i) =>
        {
            var str =
$@"        {{
            id = {i}
            position ={{ {n.Burg.x * xRatio + offset.X:0.00000} {0f:0.00000} {MapHeight - n.Burg.y * yRatio + offset.Y:0.00000} }}
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
            await File.WriteAllTextAsync("player_stack_locators.txt", file);
        }
        catch (Exception e)
        {
            Debugger.Break();
        }
    }

}
