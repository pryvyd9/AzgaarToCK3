﻿using DelaunatorSharp;
using SharpVoronoiLib;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Svg;
using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Xml;

namespace Converter;




/// <summary>
/// WritePackedHeightMap generates heightmaps starting from new line for every detail level.
/// Borders between different detail level is visible. 
/// Need to implement a better algorithm than avgN for border samples (or all).
/// </summary>
public static class HeightMapConverter
{
    private const int AzgaarWaterLevel = 50;
    public const int CK3WaterLevel = 20;

    private static readonly int[] detailSize = [33, 17, 9, 5, 3];
    // Width of average sampling for height. For detail size 9 averages of 4x4 squares are taken for every pixel in a tile.
    private static readonly byte[] averageSize = [1, 2, 4, 8, 16];
    private static readonly int maxColumnN = Settings.Instance.MapWidth / indirectionProportion;
    private static readonly int packedWidth = maxColumnN * 17;
    private const int indirectionProportion = 32;

    private static Vector2[,] Gradient(byte[] values, int width, int height)
    {
        Vector2[,] result = new Vector2[width, height];
        for (int vi = 1; vi < height; vi++)
        {
            for (int hi = 1; hi < width; hi++)
            {
                try
                {
                    // currentI
                    var ci = (height - vi - 1) * width + hi;
                    // leftI
                    var li = ci - 1;
                    // upI
                    var up = ci + width;

                    var h = values[ci] - values[li];
                    var v = values[ci] - values[up];
                    result[hi, vi] = new Vector2(h, v);
                }
                catch (Exception ex)
                {
                    Debugger.Break();
                    throw;
                }
            }
        }
        return result;
    }
    private static Vector2[,] Gradient(Vector2[,] values, int width, int height)
    {
        Vector2[,] result = new Vector2[width, height];
        for (int vi = 1; vi < height; vi++)
        {
            for (int hi = 1; hi < width; hi++)
            {
                var h = values[hi, vi].X - values[hi - 1, vi].X;
                var v = values[hi, vi].Y - values[hi, vi - 1].Y;
                result[hi, vi] = new Vector2(h, v);
            }
        }
        return result;
    }
    private static Vector2[,] GetArea(Vector2[,] values, int x, int y, int xl, int yl)
    {
        Vector2[,] result = new Vector2[xl, yl];
        for (int vi = y, j = 0; vi < y + yl; vi++, j++)
        {
            for (int hi = x, i = 0; hi < x + xl; hi++, i++)
            {
                result[i, j] = values[hi, vi];
            }
        }
        return result;
    }
    private static float GetNonZeroP90Value(Vector2[,] values)
    {
        float[] fvs = new float[values.Length * 2];

        for (int vi = 0; vi < values.GetLength(1); vi++)
        {
            for (int hi = 0; hi < values.GetLength(0); hi++)
            {
                var pos = vi * values.GetLength(0) * 2 + vi * 2;
                fvs[pos] = values[hi, vi].X;
                fvs[pos + 1] = values[hi, vi].Y;
            }
        }

        var nonZero = fvs.Where(n => n != 0).Select(Math.Abs).ToArray();
        if (nonZero.Length == 0)
        {
            return 0;
        }
        var p = Helper.Percentile(nonZero, 0.9);
        return (float)p;
    }

    private static byte[,] GetPackedArea(byte[] values, int tileI, int tileJ, int di, int height, int width)
    {
        try
        {
            var tileWidth = detailSize[di];
            var samples = new byte[tileWidth, tileWidth];

            var avgWidth = averageSize[di];
            var avgSize = avgWidth * avgWidth;

            for (int ci = tileI - indirectionProportion, i = 0; i < tileWidth; i++, ci += avgWidth)
                for (int cj = tileJ, j = 0; j < tileWidth; j++, cj += avgWidth)
                {
                    double avg = 0;
                    var avgHalfWidth = (double)avgWidth / 2;
                    var denominator = avgSize;

                    int aiFrom = (int)(ci - avgHalfWidth);
                    int aiTo = (int)(ci + avgHalfWidth);
                    if (tileI == indirectionProportion)
                    {
                        aiFrom = ci;
                        denominator /= 2;
                    }
                    else if (tileI == height)
                    {
                        aiTo = ci;
                        denominator /= 2;
                    }

                    int ajFrom = (int)(cj - avgHalfWidth);
                    int ajTo = (int)(cj + avgHalfWidth);
                    if (tileJ == 0)
                    {
                        ajFrom = cj;
                        denominator /= 2;
                    }
                    else if (tileJ == width - indirectionProportion)
                    {
                        ajTo = cj;
                        denominator /= 2;
                    }

                    for (int ai = aiFrom; ai < aiTo; ai++)
                        for (int aj = ajFrom; aj < ajTo; aj++)
                        {
                            try
                            {
                                avg += (double)values[width * ai + aj] / denominator;
                            }
                            catch (Exception e)
                            {
                                Debugger.Break();
                                throw;
                            }
                        }

                    // Transpose
                    samples[j, i] = (byte)avg;
                }

            return samples;
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }

    private class Tile
    {
        public byte[,] values;
        public int i;
        public int j;
    }
    private class Detail
    {
        //public byte[][][,] Rows;
        public Tile[][] Rows;
        public Vector2[] Coordinates;
    }
    private class PackedHeightmap
    {
        public Detail[] Details;
        public int PixelHeight;
        public int MapWidth;
        public int MapHeight;
        public int RowCount;
    }

    private static float Avg(Vector2[,] values)
    {
        float[] fvs = new float[values.Length * 2];

        for (int vi = 0; vi < values.GetLength(1); vi++)
        {
            for (int hi = 0; hi < values.GetLength(0); hi++)
            {
                var pos = vi * values.GetLength(0) * 2 + vi * 2;
                fvs[pos] = values[hi, vi].X;
                fvs[pos + 1] = values[hi, vi].Y;
            }
        }

        var p = fvs.Average();
        return (float)p;
    }

    //private static bool IsWithin(float start, float end, float target)
    //{
    //    return start <= target && target < end;
    //}

    private static async Task<PackedHeightmap> CreatePackedHeightMap(Map map)
    {
        try
        {
            const int samplesPerTile = 32;
            var mapScale = Math.Min(Settings.Instance.MapWidth, Settings.Instance.MapHeight) / 128;

            var path = $"{Settings.OutputDirectory}/map_data/heightmap.png";

            using var file = Image.Load<L8>(path);

            var pixelCount = file.Width * file.Height;
            var pixels = new byte[pixelCount];
            file.CopyPixelDataTo(pixels);

            var firstDerivative = Gradient(pixels, map.Settings.MapWidth, (int)map.Settings.MapHeight);
            var secondDerivative = Gradient(firstDerivative, map.Settings.MapWidth, (int)map.Settings.MapHeight);

            List<Vector2[,]> gradientAreas = [];
            List<byte[,]> heightAreas = [];
            List<Vector2> areaCoordinates = [];
            for (int vi = 0; vi < secondDerivative.GetLength(1); vi += samplesPerTile)
            {
                for (int hi = 0; hi < secondDerivative.GetLength(0); hi += samplesPerTile)
                {
                    gradientAreas.Add(GetArea(secondDerivative, hi, vi, samplesPerTile, samplesPerTile));
                    areaCoordinates.Add(new Vector2(hi, vi));
                }
            }

            //var weightedDerivatives = gradientAreas.Select((n, i) => (i, nonZeroP90: GetNonZeroP90Value(n), coordinates: areaCoordinates[i])).ToArray();
            var weightedDerivatives = gradientAreas.Select((n, i) => (i, nonZeroP90: Avg(n), coordinates: areaCoordinates[i])).ToArray();
            //var detail = new[]
            //{
            //    weightedDerivatives.Where(n => n.nonZeroP90 >= 4).ToArray(),
            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 3 and < 4).ToArray(),
            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 2 and < 3).ToArray(),
            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 1 and < 2).ToArray(),
            //    weightedDerivatives.Where(n => n.nonZeroP90 < 1).ToArray(),
            //};
            var detail = new[]
            {
                weightedDerivatives.Where(n => n.nonZeroP90 >= 0.01f).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 0.005f and < 0.01f).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 0.001f and < 0.005f).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 0.0005f and < 0.001f).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 < 0.0005f).ToArray(),
            };

            //float[] detailThreshold = [
            //    0.1f / mapScale,
            //    0.05f / mapScale,
            //    0.025f / mapScale,
            //    0.00625f / mapScale,
            //];

            //var detail = new[]
            //{
            //    weightedDerivatives.Where(n => n.nonZeroP90 >= detailThreshold[0]).ToArray(),
            //    weightedDerivatives.Where(n => IsWithin(detailThreshold[1], detailThreshold[0], n.nonZeroP90)).ToArray(),
            //    weightedDerivatives.Where(n => IsWithin(detailThreshold[2], detailThreshold[1], n.nonZeroP90)).ToArray(),
            //    weightedDerivatives.Where(n => IsWithin(detailThreshold[3], detailThreshold[2], n.nonZeroP90)).ToArray(),
            //    weightedDerivatives.Where(n => n.nonZeroP90 < detailThreshold[3]).ToArray(),
            //};


            //var h = 800;
            //var detail = new[]
            //{
            //     [],
            //     [],
            //     [],
            //     weightedDerivatives.Take(h).ToArray(),
            //     weightedDerivatives.Skip(h).ToArray(),
            // };

            var detailSamples = detail.Select((d, i) =>
                d.Select(n =>
                    new Tile
                    {
                        values = GetPackedArea(pixels, (int)map.Settings.MapHeight - (int)n.coordinates.Y, (int)n.coordinates.X, i, (int)map.Settings.MapHeight, (int)map.Settings.MapWidth),
                        i = (int)n.coordinates.Y / indirectionProportion,
                        j = (int)n.coordinates.X / indirectionProportion,
                    }).ToArray()
                ).ToArray();

            var dPerLine = detailSize.Select(n => packedWidth / n is var dpl && dpl > maxColumnN ? maxColumnN : dpl).ToArray();

            var details = new Detail[detail.Length];
            var maxDetailIndex = detail.Length - 1;
            var packedHeightPixels = 0;

            int? previousI = null;

            int rowCount = 0;

            for (var i = 0; i < details.Length; i++)
            {
                // Skip empty details
                if (detailSamples[i].Length == 0)
                {
                    continue;
                }

                var d = details[i] = new Detail();
                d.Coordinates = detail[i].Select(n => n.coordinates).ToArray();
                // Largest detail has fewer checks
                if (previousI == null)
                {
                    d.Rows = detailSamples[i].Chunk(dPerLine[i]).ToArray();
                }
                else
                {
                    d.Rows = detailSamples[i].Chunk(dPerLine[i]).ToArray();
                }

                packedHeightPixels += d.Rows.Length * detailSize[i];
                rowCount += d.Rows.Length;

                if (detailSamples[i].Length != 0)
                {
                    previousI = i;
                }
            }

            return new PackedHeightmap
            {
                Details = details,
                PixelHeight = packedHeightPixels,
                MapWidth = (int)map.Settings.MapWidth,
                MapHeight = (int)map.Settings.MapHeight,
                RowCount = rowCount,
            };
        }
        catch (Exception e)
        {
            Debugger.Break();
            throw;
        }
    }
    private static async Task WritePackedHeightMap(PackedHeightmap heightmap)
    {
        var horizontalTiles = heightmap.MapWidth / indirectionProportion;
        var verticalTiles = heightmap.MapHeight / indirectionProportion;
        Rgba32[] indirection_heightmap_pixels = new Rgba32[horizontalTiles * verticalTiles];
        byte[] packed_heightmap_pixels = new byte[packedWidth * heightmap.PixelHeight];

        int verticalOffset = 0;
        int[] levelOffsets = new int[5];

        for (byte di = 0; di < detailSize.Length; di++)
        {
            var d = heightmap.Details[di];
            if (d is null) continue;

            var detailColCount = packedWidth < maxColumnN * detailSize[di] ? packedWidth / detailSize[di] : maxColumnN;

            for (int ri = 0; ri < d.Rows.Length; ri++)
            {
                byte colI = 0;
                var row = d.Rows[ri];
                if (ri == 0)
                {
                    levelOffsets[di] = verticalOffset;
                }
                verticalOffset += detailSize[di];

                // tileI
                for (int ti = 0; ti < row.Length; ti++, colI++)
                {
                    var tile = row[ti];

                    for (int tx = 0; tx < detailSize[di]; tx++)
                        for (int ty = 0; ty < detailSize[di]; ty++)
                        {
                            var c = tile.values[tx, ty];
                            packed_heightmap_pixels[packedWidth * (heightmap.PixelHeight - verticalOffset + ty) + ti * detailSize[di] + tx] = c;
                        }

                    byte ihColumnIndex = colI;
                    byte ihRowIndex = (byte)ri;
                    byte ihDetailSize = averageSize[di];
                    byte ihDetailI = di;

                    try
                    {
                        indirection_heightmap_pixels[horizontalTiles * (verticalTiles - tile.i - 1) + tile.j] = new Rgba32(ihColumnIndex, ihRowIndex, ihDetailSize, ihDetailI);
                    }
                    catch (Exception ex)
                    {
                        Debugger.Break();
                        throw;
                    }
                }
            }
        }

        var packed_heightmap = Helper.ToBitmap(packed_heightmap_pixels, packedWidth, heightmap.PixelHeight);
        var phPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "packed_heightmap.png");
        Directory.CreateDirectory(Path.GetDirectoryName(phPath));
        packed_heightmap.Save(phPath);

        using var indirection_heightmap2 = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(indirection_heightmap_pixels, horizontalTiles, verticalTiles);
        var ihPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "indirection_heightmap.png");
        Directory.CreateDirectory(Path.GetDirectoryName(ihPath));
        indirection_heightmap2.Save(ihPath);

        var heightmap_heightmap = $@"heightmap_file=""map_data/packed_heightmap.png""
indirection_file=""map_data/indirection_heightmap.png""
original_heightmap_size={{ {heightmap.MapWidth} {heightmap.MapHeight} }}
tile_size=33
should_wrap_x=no
level_offsets={{ {string.Join(' ', levelOffsets.Select((n, i) => $"{{ 0 {n} }}"))} }}
max_compress_level=4
empty_tile_offset={{ 255 127 }}
";
        var hhPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.heightmap");
        Directory.CreateDirectory(Path.GetDirectoryName(hhPath));
        await File.WriteAllTextAsync(hhPath, heightmap_heightmap);
    }
    //private static async Task DrawHeightMap(Map map)
    //{
    //    try
    //    {
    //        // create ns manager
    //        XmlNamespaceManager xmlnsManager = new(map.Input.XmlMap.NameTable);
    //        xmlnsManager.AddNamespace("ns", "http://www.w3.org/2000/svg");

    //        XmlNode? GetNode(string attribute) => map.Input.XmlMap.SelectSingleNode($"//*[{attribute}]", xmlnsManager);

    //        void Remove(string attribute)
    //        {
    //            var node = map.Input.XmlMap.SelectSingleNode($"//*[{attribute}]", xmlnsManager);
    //            node?.ParentNode?.RemoveChild(node);
    //        }

    //        // Remove all blur
    //        void removeFilterFromAll(string attribute) { 
    //            for (XmlElement? element = GetNode(attribute) as XmlElement; element is not null; element = GetNode(attribute) as XmlElement)
    //            {
    //                element.RemoveAttribute("filter");
    //            }
    //        }
    //        removeFilterFromAll("@filter='url(#blur)'");
    //        removeFilterFromAll("@filter='url(#blur05)'");
    //        removeFilterFromAll("@filter='url(#blur10)'");

    //        var land = GetNode("@id='svgland'");
    //        //(land as XmlElement).RemoveAttribute("filter");
    //        var background = map.Input.XmlMap.CreateNode(XmlNodeType.Element, "rect", null);
    //        //var wl = CK3WaterLevel;
    //        var wl = 0;

    //        (background as XmlElement).SetAttribute("fill", $"rgb({wl},{wl},{wl})");
    //        (background as XmlElement).SetAttribute("x", "0");
    //        (background as XmlElement).SetAttribute("y", "0");
    //        (background as XmlElement).SetAttribute("width", "100%");
    //        (background as XmlElement).SetAttribute("height", "100%");
    //        land.PrependChild(background);

    //        //(land as XmlElement).SetAttribute("filter", "url(#blur10)");
    //        (land as XmlElement).SetAttribute("background-color", $"rgb({wl},{wl},{wl})");
    //        (land as XmlElement).SetAttribute("fill", $"rgb({wl},{wl},{wl})");

    //        // make only landHeights visible
    //        var landHeights = GetNode("@id='landHeights'") as XmlElement;
    //        landHeights.SetAttribute("filter", "url(#blur10)");
    //        //landHeights.SetAttribute("filter", "url(#blurFilter)");

    //        var rect = (landHeights.SelectSingleNode("//*[@id='landHeights']/rect") as XmlElement);
    //        rect.SetAttribute("fill", $"rgb({wl},{wl},{wl})");
    //        //rect.ParentNode.RemoveChild(rect);

    //        var blurElement = GetNode("@id='blur10'") as XmlElement;
    //        (blurElement.FirstChild as XmlElement).SetAttribute("stdDeviation", (Math.Min(Settings.Instance.MapWidth, Settings.Instance.MapHeight) / 128).ToString());

    //        var landHeightsBackground = landHeights.FirstChild;
    //        (landHeightsBackground as XmlElement).SetAttribute("fill", $"rgb({wl},{wl},{wl})");

    //        var water = GetNode("@id='water'");
    //        (water.FirstChild as XmlElement).SetAttribute("fill", $"rgb({wl},{wl},{wl})");

    //        //(GetNode("@id='vignette-mask'").FirstChild as XmlElement).SetAttribute("fill", "black");
    //        //(GetNode("@id='fog'").FirstChild as XmlElement).SetAttribute("fill", "black");


    //        // scale heights
    //        foreach (XmlElement child in landHeights.ChildNodes)
    //        {
    //            var originalFill = child.GetAttribute("fill");
    //            var originalHeight = int.Parse(new Regex(@"\((\d+)").Match(originalFill).Groups[1].Value);
    //            //const int maxHeight = 255;
    //            var newHeight = originalHeight - AzgaarWaterLevel + CK3WaterLevel;

    //            child.SetAttribute("fill", $"rgb({newHeight},{newHeight},{newHeight})");
    //        }

    //        // shadows and weird colors
    //        Remove("@id='dropShadow'");
    //        Remove("@id='dropShadow01'");
    //        Remove("@id='dropShadow05'");
    //        Remove("@id='landmass'");
    //        Remove("@id='sea_island'");
    //        Remove("@id='vignette-mask'");
    //        Remove("@id='fog'");

    //        var svg = SvgDocument.FromSvg<SvgDocument>(land.OuterXml);
    //        var img = svg.ToGrayscaleImage(map.Settings.MapWidth, map.Settings.MapHeight);

    //        var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.png");
    //        Helper.EnsureDirectoryExists(path);

    //        //img.Save("landHeights.png");
    //        img.Save(path);
    //    }
    //    catch (Exception ex)
    //    {
    //        MyConsole.Error(ex);
    //        throw;
    //    }
    //}

    private static async Task DrawHeightMap(Map map)
    {
        try
        {
            var xmlHeightmap = new XmlDocument();

            //var points = map.Input.InputMap.grid.general.points;
            //var delaunayPoints = points.Select(n => new DelaunatorSharp.Point(n[0], n[1]) as IPoint).ToArray();

            //var delaunator = new Delaunator(delaunayPoints);


            var voronoiSites = map.Input.InputMap.grid.general.points.Select(n => new VoronoiSite(n[0], n[1])).ToList();
            //var gridBoundary = map.Input.InputMap.grid.general.boundary;
            //var sites = VoronoiPlane.TessellateOnce(voronoiSites, gridBoundary[0][0], gridBoundary[0][1], gridBoundary[1][0], gridBoundary[1][1]);
            var @params = map.Input.InputMap.@params;
            //VoronoiPlane.
            var sites = VoronoiPlane.TessellateOnce(voronoiSites, 0, 0, @params.graphWidth, @params.graphHeight);

            new VoronoiSite(0,0).ClockwisePoints.Select(n =>n.x)

            var heights = map.Input.InputMap.grid.general.points;

            // create ns manager
            XmlNamespaceManager xmlnsManager = new(map.Input.XmlMap.NameTable);
            xmlnsManager.AddNamespace("ns", "http://www.w3.org/2000/svg");

            XmlNode? GetNode(string attribute) => map.Input.XmlMap.SelectSingleNode($"//*[{attribute}]", xmlnsManager);

            void Remove(string attribute)
            {
                var node = map.Input.XmlMap.SelectSingleNode($"//*[{attribute}]", xmlnsManager);
                node?.ParentNode?.RemoveChild(node);
            }

            // Remove all blur
            void removeFilterFromAll(string attribute)
            {
                for (XmlElement? element = GetNode(attribute) as XmlElement; element is not null; element = GetNode(attribute) as XmlElement)
                {
                    element.RemoveAttribute("filter");
                }
            }
            removeFilterFromAll("@filter='url(#blur)'");
            removeFilterFromAll("@filter='url(#blur05)'");
            removeFilterFromAll("@filter='url(#blur10)'");

            var land = GetNode("@id='svgland'");
            //(land as XmlElement).RemoveAttribute("filter");
            var background = map.Input.XmlMap.CreateNode(XmlNodeType.Element, "rect", null);
            //var wl = CK3WaterLevel;
            var wl = 0;

            (background as XmlElement).SetAttribute("fill", $"rgb({wl},{wl},{wl})");
            (background as XmlElement).SetAttribute("x", "0");
            (background as XmlElement).SetAttribute("y", "0");
            (background as XmlElement).SetAttribute("width", "100%");
            (background as XmlElement).SetAttribute("height", "100%");
            land.PrependChild(background);

            //(land as XmlElement).SetAttribute("filter", "url(#blur10)");
            (land as XmlElement).SetAttribute("background-color", $"rgb({wl},{wl},{wl})");
            (land as XmlElement).SetAttribute("fill", $"rgb({wl},{wl},{wl})");

            // make only landHeights visible
            var landHeights = GetNode("@id='landHeights'") as XmlElement;
            landHeights.SetAttribute("filter", "url(#blur10)");
            //landHeights.SetAttribute("filter", "url(#blurFilter)");

            var rect = (landHeights.SelectSingleNode("//*[@id='landHeights']/rect") as XmlElement);
            rect.SetAttribute("fill", $"rgb({wl},{wl},{wl})");
            //rect.ParentNode.RemoveChild(rect);

            var blurElement = GetNode("@id='blur10'") as XmlElement;
            (blurElement.FirstChild as XmlElement).SetAttribute("stdDeviation", (Math.Min(Settings.Instance.MapWidth, Settings.Instance.MapHeight) / 128).ToString());

            var landHeightsBackground = landHeights.FirstChild;
            (landHeightsBackground as XmlElement).SetAttribute("fill", $"rgb({wl},{wl},{wl})");

            var water = GetNode("@id='water'");
            (water.FirstChild as XmlElement).SetAttribute("fill", $"rgb({wl},{wl},{wl})");

            //(GetNode("@id='vignette-mask'").FirstChild as XmlElement).SetAttribute("fill", "black");
            //(GetNode("@id='fog'").FirstChild as XmlElement).SetAttribute("fill", "black");


            // scale heights
            foreach (XmlElement child in landHeights.ChildNodes)
            {
                var originalFill = child.GetAttribute("fill");
                var originalHeight = int.Parse(new Regex(@"\((\d+)").Match(originalFill).Groups[1].Value);
                //const int maxHeight = 255;
                var newHeight = originalHeight - AzgaarWaterLevel + CK3WaterLevel;

                child.SetAttribute("fill", $"rgb({newHeight},{newHeight},{newHeight})");
            }

            // shadows and weird colors
            Remove("@id='dropShadow'");
            Remove("@id='dropShadow01'");
            Remove("@id='dropShadow05'");
            Remove("@id='landmass'");
            Remove("@id='sea_island'");
            Remove("@id='vignette-mask'");
            Remove("@id='fog'");

            var svg = SvgDocument.FromSvg<SvgDocument>(land.OuterXml);
            var img = svg.ToGrayscaleImage(map.Settings.MapWidth, map.Settings.MapHeight);

            var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.png");
            Helper.EnsureDirectoryExists(path);

            //img.Save("landHeights.png");
            img.Save(path);
        }
        catch (Exception ex)
        {
            MyConsole.Error(ex);
            throw;
        }
    }


    public static async Task WriteHeightMap(Map map)
    {
        await DrawHeightMap(map);
        var heightmap = await CreatePackedHeightMap(map);
        await WritePackedHeightMap(heightmap);
    }

}