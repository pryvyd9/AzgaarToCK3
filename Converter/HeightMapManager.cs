﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Svg;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;

namespace Converter;




/// <summary>
/// WritePackedHeightMap generates heightmaps starting from new line for every detail level.
/// Borders between different detail level is visible. 
/// Need to implement a better algorithm than avgN for border samples (or all).
/// </summary>
public static class HeightMapManager
{
    private const int AzgaarWaterLevel = 50;
    private const int CK3WaterLevel = 20;

    private static readonly int[] detailSize = [33, 17, 9, 5, 3];
    // Width of average sampling for height. For detail size 9 averages of 4x4 squares are taken for every pixel in a tile.
    private static readonly byte[] averageSize = [1, 2, 4, 8, 16];
    private const int maxColumnN = Map.MapWidth / indirectionProportion;
    private const int packedWidth = (int)(maxColumnN * 17);
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
                        ajTo = ci;
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

    private static async Task<PackedHeightmap> CreatePackedHeightMap()
    {
        try
        {
            const int samplesPerTile = 32;

            var path = $"{Settings.OutputDirectory}/map_data/heightmap.png";

            var file = new Bitmap(path);
            var length8 = Map.MapWidth * Map.MapHeight * 1;
            byte[] pixels = new byte[length8];
            {
                var bitmapData = file.LockBits(new System.Drawing.Rectangle(0, 0, Map.MapWidth, Map.MapHeight), ImageLockMode.ReadOnly, file.PixelFormat);
                // Copy bitmap to byte[]
                Marshal.Copy(bitmapData.Scan0, pixels, 0, length8);
                file.UnlockBits(bitmapData);
            }


            var firstDerivative = Gradient(pixels, Map.MapWidth, Map.MapHeight);
            var secondDerivative = Gradient(firstDerivative, Map.MapWidth, Map.MapHeight);

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

            var weightedDerivatives = gradientAreas.Select((n, i) => (i, nonZeroP90: GetNonZeroP90Value(n), coordinates: areaCoordinates[i])).ToArray();
            var detail = new[]
            {
                weightedDerivatives.Where(n => n.nonZeroP90 >= 4).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 3 and < 4).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 2 and < 3).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 1 and < 2).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 < 1).ToArray(),
            };
            //var detail = new[]
            //{
            //    weightedDerivatives.Where(n => n.nonZeroP90 >= 300).ToArray(),
            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 200 and < 300).ToArray(),
            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 150 and < 200).ToArray(),
            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 100 and < 150).ToArray(),
            //    weightedDerivatives.Where(n => n.nonZeroP90 < 100).ToArray(),
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
                        values = GetPackedArea(pixels, Map.MapHeight - (int)n.coordinates.Y, (int)n.coordinates.X, i, Map.MapHeight, Map.MapWidth),
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
                MapWidth = Map.MapWidth,
                MapHeight = Map.MapHeight,
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

        var packed_heightmap = ToBitmap(packed_heightmap_pixels, packedWidth, heightmap.PixelHeight);
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
    private static async Task DrawHeightMap(Map map)
    {
        try
        {
            // create ns manager
            XmlNamespaceManager xmlnsManager = new XmlNamespaceManager(map.Input.XmlMap.NameTable);
            xmlnsManager.AddNamespace("ns", "http://www.w3.org/2000/svg");

            XmlNode? GetNode(string attribute) => map.Input.XmlMap.SelectSingleNode($"//*[{attribute}]", xmlnsManager);

            void Remove(string attribute)
            {
                var node = map.Input.XmlMap.SelectSingleNode($"//*[{attribute}]", xmlnsManager);
                node?.ParentNode?.RemoveChild(node);
            }

            // Remove all blur
            void removeFilterFromAll(string attribute) { 
                for (XmlElement? element = GetNode(attribute) as XmlElement; element is not null; element = GetNode(attribute) as XmlElement)
                {
                    element.RemoveAttribute("filter");
                }
            }
            removeFilterFromAll("@filter='url(#blur)'");
            removeFilterFromAll("@filter='url(#blur05)'");
            removeFilterFromAll("@filter='url(#blur10)'");

            var viewbox = GetNode("@id='viewbox'");
            var terrs = GetNode("@id='terrs'") as XmlElement;

            viewbox.InnerXml = null;
            viewbox.AppendChild(terrs);

            terrs.SetAttribute("filter", "url(#blur10)");

            // make only landHeights visible
            var landHeights = GetNode("@id='landHeights'") as XmlElement;
            terrs.InnerXml = null;
            terrs.AppendChild(landHeights);

            var landHeightsBackground = landHeights.SelectSingleNode($"//ns:rect[@fill='rgb(64, 64, 64)']", xmlnsManager);
            var wl = AzgaarWaterLevel - CK3WaterLevel;
            (landHeightsBackground as XmlElement).SetAttribute("fill", $"rgb({wl},{wl},{wl})");

            // scale heights
            foreach (XmlElement child in landHeights.ChildNodes)
            {
                var originalFill = child.GetAttribute("fill");
                var originalHeight = int.Parse(new Regex(@"\((\d+)").Match(originalFill).Groups[1].Value);
                const int maxHeight = 255;
                //var newHeight = (originalHeight * (255 - WaterLevelHeight) / maxHeight + WaterLevelHeight);
                //var newHeight = (originalHeight * (255 - WaterLevelHeight) / maxHeight);
                var newHeight = originalHeight - AzgaarWaterLevel + CK3WaterLevel;

                child.SetAttribute("fill", $"rgb({newHeight},{newHeight},{newHeight})");
            }

            // set blur value
            //var blurValue = 10;
            var blurValue = 8;
            GetNode("@id='blur10'").InnerXml = $"<feGaussianBlur in=\"SourceGraphic\" stdDeviation=\"{blurValue}\" />";


            // shadows and weird colors
            Remove("@id='dropShadow'");
            Remove("@id='dropShadow01'");
            Remove("@id='dropShadow05'");
            Remove("@id='landmass'");
            Remove("@id='sea_island'");

            var svg = SvgDocument.Open(map.Input.XmlMap);
            var bitmap = svg.Draw(Map.MapWidth, Map.MapHeight);

            var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var bitmap8 = ToGrayscale(bitmap);

            bitmap8.Save(path, ImageFormat.Png);
        }
        catch (Exception ex)
        {
            MyConsole.Error(ex);
            throw;
        }
    }

    public static unsafe Bitmap ToGrayscale(Bitmap colorBitmap)
    {
        int Width = colorBitmap.Width;
        int Height = colorBitmap.Height;

        Bitmap grayscaleBitmap = new Bitmap(Width, Height, PixelFormat.Format8bppIndexed);

        grayscaleBitmap.SetResolution(colorBitmap.HorizontalResolution,
                             colorBitmap.VerticalResolution);

        ///////////////////////////////////////
        // Set grayscale palette
        ///////////////////////////////////////
        ColorPalette colorPalette = grayscaleBitmap.Palette;
        for (int i = 0; i < colorPalette.Entries.Length; i++)
        {
            colorPalette.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
        }
        grayscaleBitmap.Palette = colorPalette;
        ///////////////////////////////////////
        // Set grayscale palette
        ///////////////////////////////////////
        BitmapData bitmapData = grayscaleBitmap.LockBits(
            new System.Drawing.Rectangle(System.Drawing.Point.Empty, grayscaleBitmap.Size),
            ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

        Byte* pPixel = (Byte*)bitmapData.Scan0;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                System.Drawing.Color clr = colorBitmap.GetPixel(x, y);

                Byte byPixel = (byte)((30 * clr.R + 59 * clr.G + 11 * clr.B) / 100);

                pPixel[x] = byPixel;
            }

            pPixel += bitmapData.Stride;
        }

        grayscaleBitmap.UnlockBits(bitmapData);

        return grayscaleBitmap;
    }

    public static unsafe Bitmap ToBitmap(byte[] colorBitmap, int width, int height)
    {
        Bitmap grayscaleBitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

        ///////////////////////////////////////
        // Set grayscale palette
        ///////////////////////////////////////
        ColorPalette colorPalette = grayscaleBitmap.Palette;
        for (int i = 0; i < colorPalette.Entries.Length; i++)
        {
            colorPalette.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
        }
        grayscaleBitmap.Palette = colorPalette;
        ///////////////////////////////////////
        // Set grayscale palette
        ///////////////////////////////////////
        BitmapData bitmapData = grayscaleBitmap.LockBits(
            new System.Drawing.Rectangle(System.Drawing.Point.Empty, grayscaleBitmap.Size),
            ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

        Byte* pPixel = (Byte*)bitmapData.Scan0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pPixel[x] = colorBitmap[width * y + x];
            }

            pPixel += bitmapData.Stride;
        }

        grayscaleBitmap.UnlockBits(bitmapData);

        return grayscaleBitmap;
    }


    public static async Task WriteHeightMap(Map map)
    {
        await DrawHeightMap(map);
        var heightmap = await CreatePackedHeightMap();
        await WritePackedHeightMap(heightmap);
    }

}