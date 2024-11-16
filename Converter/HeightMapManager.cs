using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics;
using System.Numerics;

namespace Converter;




/// <summary>
/// WritePackedHeightMap generates heightmaps starting from new line for every detail level.
/// Borders between different detail level is visible. 
/// Need to implement a better algorithm than avgN for border samples (or all).
/// </summary>
public static class HeightMapManager
{
    private const int WaterLevelHeight = 30;

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
            using var file = new MagickImageCollection(path);
            var heightMap = file[0];

            // the values go like this: grayscale, alpha. We only need grayscale.
            var pixels = heightMap.GetPixels().GetValues().Where((n, i) => i % 2 == 0).ToArray();

            var firstDerivative = Gradient(pixels, heightMap.Width, heightMap.Height);
            var secondDerivative = Gradient(firstDerivative, heightMap.Width, heightMap.Height);

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
                MapWidth = heightMap.Width,
                MapHeight = heightMap.Height,
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
        using var packed_heightmap = new MagickImage("xc:black", new MagickReadSettings()
        {
            Width = packedWidth,
            Height = heightmap.PixelHeight,
        });
        var phDrawables = new Drawables();

        var horizontalTiles = heightmap.MapWidth / indirectionProportion;
        var verticalTiles = heightmap.MapHeight / indirectionProportion;
        Rgba32[] indirection_heightmap_pixels = new Rgba32[horizontalTiles * verticalTiles];

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
                            var phColor = new MagickColor(c, c, c);
                            phDrawables
                                .DisableStrokeAntialias()
                                .StrokeColor(phColor)
                                .FillColor(phColor)
                                .Point(ti * detailSize[di] + tx, heightmap.PixelHeight - verticalOffset + ty);
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

        packed_heightmap.Draw(phDrawables);
        var phPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "packed_heightmap.png");
        Directory.CreateDirectory(Path.GetDirectoryName(phPath));
        await packed_heightmap.WriteAsync(phPath);

        using var indirection_heightmap2 = Image.LoadPixelData<Rgba32>(indirection_heightmap_pixels, horizontalTiles, verticalTiles);
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
            var settings = new MagickReadSettings()
            {
                Width = Map.MapWidth,
                Height = Map.MapHeight,
            };
            using var cellsMap = new MagickImage("xc:black", settings);

            var drawables = new Drawables();

            var landPackCells = map.Input.JsonMap.pack.cells.Where(n => n.biome != 0);
            var landGeoCells = map.Input.GeoMap.features.Select(n => new
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
                        .Polygon(cell.Select(n => Helper.GeoToPixel(n[0], n[1], map)));
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

    public static async Task WriteHeightMap(Map map)
    {
        await DrawHeightMap(map);
        var heightmap = await CreatePackedHeightMap();
        await WritePackedHeightMap(heightmap);
    }

}