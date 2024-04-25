using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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

            var cells = feature.geometry.coordinates.Select(n => new Cell(feature.properties.height, n));
            provinces[provinceId].Cells.AddRange(cells);
        }
        return provinces;
    }
    
    private static Province[] CreateProvinces(GeoMap geomap, JsonMap jsonmap)
    {
        var provinceCells = GetProvinceCells(geomap);
        var provinces = new Province[provinceCells.Count];

        // pId == 0 is ocean.
        provinces[0] = provinceCells[0];
        provinces[0].Color = MagickColors.Black;
        provinces[0].Name = "x";

        int i = 1;
        try
        {
            for (; i < provinces.Length; i++)
            {
                var color = GetColor(i, provinces.Length);
                provinces[i] = provinceCells[i];
                provinces[i].Color = color;
                provinces[i].Name = jsonmap.pack.provinces[i].name;
            }
        }
        catch(Exception ex)
        {
            Debugger.Break();
        }
       

        return provinces;
    }

    public static async Task<Map> ConvertMap(GeoMap geoMap, JsonMap jsonMap)
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
                        .Polygon(cell.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)))
                        .DisableStrokeAntialias()
                        .StrokeWidth(2)
                        .StrokeColor(MagickColors.Black)
                        .FillOpacity(new Percentage(0));
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
                        .Polygon(cell.cells.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)))
                        .DisableStrokeAntialias()
                        .StrokeColor(province.Color)
                        .FillColor(province.Color);
                }
            }

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
            var minHeight = map.Provinces.Skip(1).SelectMany(n => n.Cells).MinBy(n => n.height)!.height;

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
                    if (trimmedHeight is > 255 or < WaterLevelHeight)
                    {
                        int i = 0;
                    }

                    var culledHeight = (byte)trimmedHeight;

                    if (culledHeight < WaterLevelHeight)
                    {
                        int i = 0;
                    }

                    var color = new MagickColor(culledHeight, culledHeight, culledHeight);
                    drawables
                        .Polygon(cell.cells.Select(n => new PointD((n[0] - map.XOffset) * map.XRatio, MapHeight - (n[1] - map.YOffset) * map.YRatio)))
                        .DisableStrokeAntialias()
                        .StrokeColor(color)
                        .FillColor(color);
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
}
