using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Numerics;

namespace Converter;
/// <summary>
/// WritePackedHeightSimple generates simple heightmap and the fastest one.
/// WritePackedHeightMap generates heightmaps starting from new line for every detail level.
/// At some point the game started crashing the game unless a single detail level is is written.
/// 
/// The commented out PackedMapManager writes heightmap with proper horizontal offsets on the same lines.
/// But the height is read incorrectly.
/// </summary>
public static class PackedMapManager
{
    private static readonly int[] detailSize = [31, 17, 9, 5, 3];
    private static readonly int[] averageSize = [1, 2, 4, 8, 16];
    private const int maxColumnN = 256;
    //private const int maxColumnN = 64;
    private const int packedWidth = (int)(maxColumnN * 9);
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
    // 31 isn't working yet
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
                    var avgHalfWidth = avgWidth / 2;
                    var denominator = avgSize;

                    int aiFrom = ci - avgHalfWidth;
                    int aiTo = ci + avgHalfWidth;
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

                    int ajFrom = cj - avgHalfWidth;
                    int ajTo = cj + avgHalfWidth;
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
                    {
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


    public class Tile
    {
        public byte[,] values;
        public int i;
        public int j;
    }
    public class Detail
    {
        //public byte[][][,] Rows;
        public Tile[][] Rows;
        public Vector2[] Coordinates;
    }
    public class PackedHeightmap
    {
        public Detail[] Details;
        public int PixelHeight;
        public int MapWidth;
        public int MapHeight;
        public int RowCount;
    }
  
    public static async Task<PackedHeightmap> CreatePackedHeightMap()
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
                    //heightAreas.Add(GetArea(pixels, hi, vi, samplesPerTile, samplesPerTile, heightMap.Width, heightMap.Height));
                    areaCoordinates.Add(new Vector2(hi, vi));
                }
            }

            //var weightedDerivatives = gradientAreas.Select((n, i) => (i, heightArea: heightAreas[i], nonZeroP90: GetNonZeroP90Value(n), coordinates: areaCoordinates[i])).ToArray();
            var weightedDerivatives = gradientAreas.Select((n, i) => (i, nonZeroP90: GetNonZeroP90Value(n), coordinates: areaCoordinates[i])).ToArray();
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
                 weightedDerivatives.Where(n => false).ToArray(),
                 weightedDerivatives.Where(n => false).ToArray(),
                 weightedDerivatives.Where(n => true).ToArray(),
                 weightedDerivatives.Where(n => false).ToArray(),
                 weightedDerivatives.Where(n => false).ToArray(),
             };

            var detailSamples = detail.Select((d, i) =>
                d.Select(n =>
                    new Tile
                    {
                        values = GetPackedArea(pixels, Map.MapHeight - (int)n.coordinates.Y, (int)n.coordinates.X, i, Map.MapHeight, Map.MapWidth),
                        i = (int)n.coordinates.Y / indirectionProportion,
                        j = (int)n.coordinates.X / indirectionProportion,
                    }).ToArray()
                )
                .ToArray();

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
    public static async Task WritePackedHeightMap(PackedHeightmap heightmap)
    {
        byte[] someNumbers = [1, 2, 4, 8, 16];

        using var packed_heightmap = new MagickImage("xc:black", new MagickReadSettings()
        {
            Width = packedWidth,
            Height = heightmap.PixelHeight,
        });
        var phDrawables = new Drawables();

        Rgba32[] indirection_heightmap_pixelArray = new Rgba32[heightmap.MapWidth / indirectionProportion * heightmap.MapHeight / indirectionProportion];

        int verticalOffset = 0;
        int rowI = 0;

        bool isWhite = false;

        int[] levelOffsets = new int[5];

        for (byte di = 0; di < detailSize.Length; di++)
        {
            var d = heightmap.Details[di];
            if (d is null) continue;

            // coordinatesI
            int ci = 0;
            //byte ColI = 0;
            byte colI = 0;

            for (int li = 0; li < d.Rows.Length; li++)
            {
                colI = 0;
                var row = d.Rows[li];
                if (li == 0)
                {
                    levelOffsets[di] = verticalOffset;
                }
                verticalOffset += detailSize[di];
                rowI++;

                // tileI
                for (int ti = 0; ti < row.Length; ti++, ci++, colI++)
                {
                    var tile = row[ti];

                    for (int tx = 0; tx < detailSize[di]; tx++)
                    {
                        for (int ty = 0; ty < detailSize[di]; ty++)
                        {
                            var c = tile.values[tx, ty];
                            var phColor = new MagickColor(c, c, c);
                            //phColor = isWhite ? new MagickColor(255, 255, 255) : new MagickColor(100, 100, 100);
                            phDrawables
                                .DisableStrokeAntialias()
                                .StrokeColor(phColor)
                                .FillColor(phColor)
                                .Point(ti * detailSize[di] + tx, heightmap.PixelHeight - verticalOffset + ty);
                        }
                    }

                    byte ihColumnIndex = (byte)(colI);
                    byte ihRowIndex = (byte)(heightmap.RowCount - rowI);
                    byte ihDetailSize = someNumbers[di];
                    byte ihDetailI = di;
                    var ihColor = new MagickColor(ihColumnIndex, ihRowIndex, ihDetailSize, ihDetailI);

                    isWhite = !isWhite;

                    try
                    {
                        //var ihXY = d.Coordinates[ci] / indirectionProportion;
                        //indirection_heightmap_pixelArray[(heightmap.MapWidth / indirectionProportion) * (int)ihXY.Y + (int)ihXY.X] = new Rgba32(ihColumnIndex, ihRowIndex, ihDetailSize, ihDetailI);
                        indirection_heightmap_pixelArray[(heightmap.MapWidth / indirectionProportion) * tile.i + tile.j] = new Rgba32(ihColumnIndex, ihRowIndex, ihDetailSize, ihDetailI);
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

        using var indirection_heightmap2 = Image.LoadPixelData<Rgba32>(indirection_heightmap_pixelArray, heightmap.MapWidth / indirectionProportion, heightmap.MapHeight / indirectionProportion);
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

    // Detail level = 3
    // All tiles have the same detail level
    public static async Task WritePackedHeightSimple(Map map)
    {
        File.Copy(Helper.GetPath(SettingsManager.ExecutablePath, "indirection_heightmap.png"), Helper.GetPath(Settings.OutputDirectory, "map_data", "indirection_heightmap.png"), true);

        using var heightmap = new MagickImageCollection(Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.png"));
        heightmap[0].InterpolativeResize(256 * 31, 128 * 31, PixelInterpolateMethod.Spline);
        var phPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "packed_heightmap.png");
        await heightmap.WriteAsync(phPath);

        var heightmap_heightmap = $@"heightmap_file=""map_data/packed_heightmap.png""
indirection_file=""map_data/indirection_heightmap.png""
original_heightmap_size={{ {Map.MapWidth} {Map.MapHeight} }}
tile_size=33
should_wrap_x=no
level_offsets={{ {{ 0 0 }} {{ 0 0 }} {{ 0 0 }} {{ 0 0 }} {{ 0 0 }} }}
max_compress_level=4
empty_tile_offset={{ 255 127 }}
";
        var hhPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.heightmap");
        await File.WriteAllTextAsync(hhPath, heightmap_heightmap);
    }
}

//public static class PackedMapManager
//{
//    private static readonly int[] detailSize = [31, 17, 9, 5, 3];
//    private static readonly int[] averageSize = [1, 2, 4, 8, 16];
//    //private const int maxColumnN = 256;
//    private const int maxColumnN = 64;
//    private const int packedWidth = (int)(maxColumnN * 3) - 5;
//    private const int indirectionProportion = 32;

//    private static Vector2[,] Gradient(byte[] values, int width, int height)
//    {
//        Vector2[,] result = new Vector2[width, height];
//        for (int vi = 1; vi < height; vi++)
//        {
//            for (int hi = 1; hi < width; hi++)
//            {
//                try
//                {
//                    // currentI
//                    var ci = (height - vi - 1) * width + hi;
//                    // leftI
//                    var li = ci - 1;
//                    // upI
//                    var up = ci + width;

//                    var h = values[ci] - values[li];
//                    var v = values[ci] - values[up];
//                    result[hi, vi] = new Vector2(h, v);
//                }
//                catch (Exception ex)
//                {
//                    Debugger.Break();
//                    throw;
//                }
//            }
//        }
//        return result;
//    }
//    private static Vector2[,] Gradient(Vector2[,] values, int width, int height)
//    {
//        Vector2[,] result = new Vector2[width, height];
//        for (int vi = 1; vi < height; vi++)
//        {
//            for (int hi = 1; hi < width; hi++)
//            {
//                var h = values[hi, vi].X - values[hi - 1, vi].X;
//                var v = values[hi, vi].Y - values[hi, vi - 1].Y;
//                result[hi, vi] = new Vector2(h, v);
//            }
//        }
//        return result;
//    }
//    private static Vector2[,] GetArea(Vector2[,] values, int x, int y, int xl, int yl)
//    {
//        Vector2[,] result = new Vector2[xl, yl];
//        for (int vi = y, j = 0; vi < y + yl; vi++, j++)
//        {
//            for (int hi = x, i = 0; hi < x + xl; hi++, i++)
//            {
//                result[i, j] = values[hi, vi];
//            }
//        }
//        return result;
//    }
//    private static byte[,] GetArea(byte[] values, int x, int y, int xl, int yl, int width, int height)
//    {
//        byte[,] result = new byte[xl, yl];

//        // Packed heightmap duplicates end of one tile and start of the next tile.
//        // Only do it when it's not the first tile.
//        bool isFirstRow = y > 0;

//        for (int vi = y + yl - 1, j = 0; j < yl; vi--, j++)
//        {
//            bool isFirstColumn = x > 0;

//            for (int hi = x, i = 0; i < xl; hi++, i++)
//            {
//                try
//                {
//                    var viv = isFirstRow ? vi - 11 : vi;
//                    var hiv = isFirstColumn ? hi - 11 : hi;
//                    // currentI
//                    var ci = (height - viv - 1) * width + hiv;
//                    result[i, j] = values[ci];
//                }
//                catch (Exception e)
//                {
//                    Debugger.Break();
//                    throw;
//                }
//                isFirstColumn = false;
//            }
//            isFirstRow = false;
//        }
//        return result;
//    }



//    private static float GetNonZeroP90Value(Vector2[,] values)
//    {
//        float[] fvs = new float[values.Length * 2];

//        for (int vi = 0; vi < values.GetLength(1); vi++)
//        {
//            for (int hi = 0; hi < values.GetLength(0); hi++)
//            {
//                var pos = vi * values.GetLength(0) * 2 + vi * 2;
//                fvs[pos] = values[hi, vi].X;
//                fvs[pos + 1] = values[hi, vi].Y;
//            }
//        }

//        var nonZero = fvs.Where(n => n != 0).Select(Math.Abs).ToArray();
//        if (nonZero.Length == 0)
//        {
//            return 0;
//        }
//        var p = Helper.Percentile(nonZero, 0.9);
//        return (float)p;
//    }

//    //private static float GetMaxValue(Vector2[,] values)
//    //{
//    //    float max = 0;

//    //    for (int vi = 0; vi < values.GetLength(1); vi++)
//    //    {
//    //        for (int hi = 0; hi < values.GetLength(0); hi++)
//    //        {
//    //            if (values[hi, vi].X > max) max = values[hi, vi].X;
//    //            if (values[hi, vi].Y > max) max = values[hi, vi].Y;
//    //        }
//    //    }

//    //    return max;
//    //}
//    //private static float GetMaxValue(Vector2[,] values)
//    //{
//    //    float max = 0;

//    //    for (int vi = 0; vi < values.GetLength(1); vi++)
//    //    {
//    //        for (int hi = 0; hi < values.GetLength(0); hi++)
//    //        {
//    //            if (Math.Abs(values[hi, vi].X) > max) max = Math.Abs(values[hi, vi].X);
//    //            if (Math.Abs(values[hi, vi].Y) > max) max = Math.Abs(values[hi, vi].Y);
//    //        }
//    //    }

//    //    return max;
//    //}
//    //private static float GetAverageValue(Vector2[,] values)
//    //{
//    //    float sum = 0;
//    //    //float area = values.GetLength(1) * values.GetLength(0);

//    //    for (int vi = 0; vi < values.GetLength(1); vi++)
//    //    {
//    //        for (int hi = 0; hi < values.GetLength(0); hi++)
//    //        {
//    //            sum += Math.Abs(values[hi, vi].X) + Math.Abs(values[hi, vi].Y);
//    //        }
//    //    }

//    //    return sum;
//    //}

//    private static byte[,] GetPackedValues(byte[,] values, int horizontalSampleCount)
//    {
//        var samples = new byte[horizontalSampleCount, horizontalSampleCount];
//        var step = values.GetLength(0) / horizontalSampleCount;
//        for (int vi = 0, j = 0; j < horizontalSampleCount; vi += step, j++)
//        {
//            for (int hi = 0, i = 0; i < horizontalSampleCount; hi += step, i++)
//            {
//                samples[i, j] = values[hi, vi];
//            }
//        }
//        return samples;
//    }

//    private static byte GetAverage(byte[,] values)
//    {
//        double avg = 0;
//        var totalCount = values.LongLength;

//        foreach (var v in values)
//        {
//            avg += (double)v / totalCount;
//        }

//        return (byte) avg;
//    }

//    // 31 isn't working yet
//    private static byte[,] GetPackedArea(byte[] values, int tileI, int tileJ, int di, int height, int width)
//    {
//        try
//        {
//            var tileWidth = detailSize[di];
//            var samples = new byte[tileWidth, tileWidth];

//            var avgWidth = averageSize[di];
//            var avgSize = avgWidth * avgWidth;

//            //for (int ci = tileI - indirectionProportion, i = 0; i < tileWidth; i++, ci += avgWidth)
//            for (int ci = tileI, i = 0; i < tileWidth; i++, ci += avgWidth)
//                for (int cj = tileJ, j = 0; j < tileWidth; j++, cj += avgWidth)
//                {
//                    double avg = 0;
//                    var avgHalfWidth = avgWidth / 2;
//                    var denominator = avgSize;

//                    int aiFrom = ci - avgHalfWidth;
//                    int aiTo = ci + avgHalfWidth;
//                    if (tileI == 0)
//                    {
//                        aiFrom = ci;
//                        denominator /= 2;
//                    }
//                    else if (tileI == height - indirectionProportion)
//                    {
//                        aiTo = ci;
//                        denominator /= 2;
//                    }

//                    int ajFrom = cj - avgHalfWidth;
//                    int ajTo = cj + avgHalfWidth;
//                    if (tileJ == 0)
//                    {
//                        ajFrom = cj;
//                        denominator /= 2;
//                    }
//                    else if (tileJ == width - indirectionProportion)
//                    {
//                        ajTo = ci;
//                        denominator /= 2;
//                    }

//                    for (int ai = aiFrom; ai < aiTo; ai++)
//                    {
//                        for (int aj = ajFrom; aj < ajTo; aj++)
//                        {
//                            try
//                            {
//                                avg += (double)values[width * ai + aj] / denominator;
//                            }
//                            catch (Exception e)
//                            {
//                                Debugger.Break();
//                                throw;
//                            }
//                        }
//                    }

//                    // Transpose
//                    samples[j, i] = (byte)avg;
//                }

//            return samples;
//        }
//        catch (Exception ex)
//        {
//            Debugger.Break();
//            throw;
//        }
//    }


//    public class PackedHeightmap
//    {
//        public Detail[] Details;
//        public int PixelHeight;
//        public int MapWidth;
//        public int MapHeight;
//        public int RowCount;
//    }
//    public class Detail
//    {
//        public byte[][][,] Rows;
//        public Vector2[] Coordinates;
//    }
//    public static async Task<PackedHeightmap> CreatePackedHeightMap()
//    {
//        try
//        {
//            const int samplesPerTile = 32;

//            var path = $"{Settings.OutputDirectory}/map_data/heightmap.png";
//            using var file = new MagickImageCollection(path);
//            var heightMap = file[0];

//            // the values go like this: grayscale, alpha. We only need grayscale.
//            var pixels = heightMap.GetPixels().GetValues().Where((n, i) => i % 2 == 0).ToArray();

//            var firstDerivative = Gradient(pixels, heightMap.Width, heightMap.Height);
//            var secondDerivative = Gradient(firstDerivative, heightMap.Width, heightMap.Height);

//            List<Vector2[,]> gradientAreas = [];
//            List<byte[,]> heightAreas = [];
//            List<Vector2> areaCoordinates = [];
//            for (int vi = 0; vi < secondDerivative.GetLength(1); vi += samplesPerTile)
//            {
//                for (int hi = 0; hi < secondDerivative.GetLength(0); hi += samplesPerTile)
//                {
//                    gradientAreas.Add(GetArea(secondDerivative, hi, vi, samplesPerTile, samplesPerTile));
//                    //heightAreas.Add(GetArea(pixels, hi, vi, samplesPerTile, samplesPerTile, heightMap.Width, heightMap.Height));
//                    areaCoordinates.Add(new Vector2(hi, vi));
//                }
//            }

//            //var weightedDerivatives = gradientAreas.Select((n, i) => (i, heightArea: heightAreas[i], nonZeroP90: GetNonZeroP90Value(n), coordinates: areaCoordinates[i])).ToArray();
//            var weightedDerivatives = gradientAreas.Select((n, i) => (i, nonZeroP90: GetNonZeroP90Value(n), coordinates: areaCoordinates[i])).ToArray();
//            //var detail = new[]
//            //{
//            //    weightedDerivatives.Where(n => n.nonZeroP90 >= 4).ToArray(),
//            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 3 and < 4).ToArray(),
//            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 2 and < 3).ToArray(),
//            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 1 and < 2).ToArray(),
//            //    weightedDerivatives.Where(n => n.nonZeroP90 < 1).ToArray(),
//            //};
//            var detail = new[]
//            {
//                 weightedDerivatives.Where(n => false).ToArray(),
//                 weightedDerivatives.Where(n => false).ToArray(),
//                 weightedDerivatives.Where(n => false).ToArray(),
//                 weightedDerivatives.Where(n => false).ToArray(),
//                 weightedDerivatives.Where(n => true).ToArray(),
//             };

//            //var detailSamples = detail.Select((d, i) => d.Select(n => GetPackedValues(n.heightArea, detailSize[i])).ToArray()).ToArray();
//            //var detailSamples = detail.Select((d, i) => d.Select(n => GetPackedArea(pixels, Map.MapHeight -  (int)n.coordinates.Y, (int)n.coordinates.X, i, Map.MapHeight, Map.MapWidth)).ToArray()).ToArray();
//            var detailSamples = detail.Select((d, i) => 
//                d.Select(n => 
//                    //GetPackedArea(pixels, Map.MapHeight -  (int)n.coordinates.Y, (int)n.coordinates.X, i, Map.MapHeight, Map.MapWidth))
//                    GetPackedArea(pixels, (int)n.coordinates.Y, (int)n.coordinates.X, i, Map.MapHeight, Map.MapWidth))
//                    .Reverse()
//                    .ToArray()
//                )
//                .ToArray();

//            var dPerLine = detailSize.Select(n => packedWidth / n is var dpl && dpl > maxColumnN ? maxColumnN : dpl).ToArray();

//            var details = new Detail[detail.Length];
//            var maxDetailIndex = detail.Length - 1;
//            var packedHeightPixels = 0;

//            int? previousI = null;

//            int lineCount = 0;

//            for (var i = 0; i < details.Length; i++)
//            {
//                // Skip empty details
//                if (detailSamples[i].Length == 0)
//                {
//                    continue;
//                }

//                var d = details[i] = new Detail();
//                d.Coordinates = detail[i].Select(n => n.coordinates).ToArray();
//                // Largest detail has fewer checks
//                if (previousI == null)
//                {
//                    d.Rows = detailSamples[i].Chunk(dPerLine[i]).ToArray();
//                }
//                else
//                {
//                    d.Rows = detailSamples[i].Chunk(dPerLine[i]).ToArray();

//                }

//                packedHeightPixels += d.Rows.Length * detailSize[i];
//                lineCount += d.Rows.Length;

//                if (detailSamples[i].Length != 0)
//                {
//                    previousI = i;
//                }
//            }

//            return new PackedHeightmap
//            {
//                Details = details,
//                PixelHeight = packedHeightPixels,
//                MapWidth = heightMap.Width,
//                MapHeight = heightMap.Height,
//                RowCount = lineCount,
//            };
//        }
//        catch (Exception e)
//        {
//            Debugger.Break();
//            throw;
//        }
//    }
//    public static async Task WritePackedHeightMap(PackedHeightmap heightmap)
//    {
//        byte[] someNumbers = [1, 2, 4, 8, 16];

//        using var packed_heightmap = new MagickImage("xc:black", new MagickReadSettings()
//        {
//            Width = packedWidth,
//            Height = heightmap.PixelHeight,
//        });
//        var phDrawables = new Drawables();

//        Rgba32[] indirection_heightmap_pixelArray = new Rgba32[heightmap.MapWidth / indirectionProportion * heightmap.MapHeight / indirectionProportion];

//        int verticalOffset = 0;
//        int rowI = 0;

//        bool isWhite = false;

//        int[] levelOffsets = new int[5];

//        for (byte di = 0; di < detailSize.Length; di++)
//        {
//            var d = heightmap.Details[di];
//            if (d is null) continue;

//            // coordinatesI
//            int ci = 0;
//            //byte ColI = 0;
//            byte colI = 0;

//            for (int li = 0; li < d.Rows.Length; li++)
//            {
//                colI = 0;
//                var row = d.Rows[li];
//                if (li == 0)
//                {
//                    levelOffsets[di] = verticalOffset;
//                }
//                verticalOffset += detailSize[di];
//                rowI++;

//                // tileI
//                for (int ti = 0; ti < row.Length; ti++, ci++, colI++)
//                {
//                    var sample = row[ti];

//                    for (int tx = 0; tx < detailSize[di]; tx++)
//                    {
//                        for (int ty = 0; ty < detailSize[di]; ty++)
//                        {
//                            var c = sample[tx, ty];
//                            var phColor = new MagickColor(c, c, c);
//                            //phColor = isWhite ? new MagickColor(255, 255, 255) : new MagickColor(100, 100, 100);
//                            phDrawables
//                                .DisableStrokeAntialias()
//                                .StrokeColor(phColor)
//                                .FillColor(phColor)
//                                .Point(ti * detailSize[di] + tx, heightmap.PixelHeight - verticalOffset + ty);
//                        }
//                    }

//                    byte ihColumnIndex = (byte)(row.Length - colI);
//                    byte ihRowIndex = (byte)(heightmap.RowCount - rowI);
//                    byte ihDetailSize = someNumbers[di];
//                    byte ihDetailI = di;
//                    var ihColor = new MagickColor(ihColumnIndex, ihRowIndex, ihDetailSize, ihDetailI);

//                    isWhite = !isWhite;

//                    var ihXY = d.Coordinates[ci] / indirectionProportion;
//                    indirection_heightmap_pixelArray[(heightmap.MapWidth / indirectionProportion) * (int)ihXY.Y + (int)ihXY.X] = new Rgba32(ihColumnIndex, ihRowIndex, ihDetailSize, ihDetailI);
//                }
//            }
//        }

//        packed_heightmap.Draw(phDrawables);
//        var phPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "packed_heightmap.png");
//        Directory.CreateDirectory(Path.GetDirectoryName(phPath));
//        await packed_heightmap.WriteAsync(phPath);

//        using var indirection_heightmap2 = Image.LoadPixelData<Rgba32>(indirection_heightmap_pixelArray, heightmap.MapWidth / indirectionProportion, heightmap.MapHeight / indirectionProportion);
//        var ihPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "indirection_heightmap.png");
//        Directory.CreateDirectory(Path.GetDirectoryName(ihPath));
//        indirection_heightmap2.Save(ihPath);

//        var heightmap_heightmap = $@"heightmap_file=""map_data/packed_heightmap.png""
//indirection_file=""map_data/indirection_heightmap.png""
//original_heightmap_size={{ {heightmap.MapWidth} {heightmap.MapHeight} }}
//tile_size=33
//should_wrap_x=no
//level_offsets={{ {string.Join(' ', levelOffsets.Select((n, i) => $"{{ 0 {n} }}"))} }}
//max_compress_level=4
//empty_tile_offset={{ 255 127 }}
//";
//        var hhPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.heightmap");
//        Directory.CreateDirectory(Path.GetDirectoryName(hhPath));
//        await File.WriteAllTextAsync(hhPath, heightmap_heightmap);
//    }

//    // Detail level = 3
//    // All tiles have the same detail level
//    public static async Task WritePackedHeightSimple(Map map)
//    {
//        File.Copy(Helper.GetPath(SettingsManager.ExecutablePath, "indirection_heightmap.png"), Helper.GetPath(Settings.OutputDirectory, "map_data", "indirection_heightmap.png"), true);

//        using var heightmap = new MagickImageCollection(Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.png"));
//        heightmap[0].InterpolativeResize(256 * 31, 128 * 31, PixelInterpolateMethod.Spline);
//        var phPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "packed_heightmap.png");
//        await heightmap.WriteAsync(phPath);

//        var heightmap_heightmap = $@"heightmap_file=""map_data/packed_heightmap.png""
//indirection_file=""map_data/indirection_heightmap.png""
//original_heightmap_size={{ {Map.MapWidth} {Map.MapHeight} }}
//tile_size=33
//should_wrap_x=no
//level_offsets={{ {{ 0 0 }} {{ 0 0 }} {{ 0 0 }} {{ 0 0 }} {{ 0 0 }} }}
//max_compress_level=4
//empty_tile_offset={{ 255 127 }}
//";
//        var hhPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.heightmap");
//        await File.WriteAllTextAsync(hhPath, heightmap_heightmap);
//    }
//}



//public static class PackedMapManager
//{
//    private static readonly int[] detailSize = [31, 15, 9, 5, 3];
//    //private const int packedWidth = 264;
//    private const int packedWidth = 768;
//    private const int indirectionProportion = 32;

//    private static Vector2[,] Gradient(byte[] values, int width, int height)
//    {
//        Vector2[,] result = new Vector2[width, height];
//        for (int vi = 1; vi < height; vi++)
//        {
//            for (int hi = 1; hi < width; hi++)
//            {
//                // currentI
//                var ci = vi * width + hi;
//                // leftI
//                var li = ci - 1;
//                // upI
//                var up = ci - width;

//                var h = values[ci] - values[li];
//                var v = values[ci] - values[up];
//                result[hi, vi] = new Vector2(h, v);
//            }
//        }
//        return result;
//    }
//    private static Vector2[,] Gradient(Vector2[,] values, int width, int height)
//    {
//        Vector2[,] result = new Vector2[width, height];
//        for (int vi = 1; vi < height; vi++)
//        {
//            for (int hi = 1; hi < width; hi++)
//            {
//                var h = values[hi, vi].X - values[hi - 1, vi].X;
//                var v = values[hi, vi].Y - values[hi, vi - 1].Y;
//                result[hi, vi] = new Vector2(h, v);
//            }
//        }
//        return result;
//    }
//    private static Vector2[,] GetArea(Vector2[,] values, int x, int y, int xl, int yl)
//    {
//        Vector2[,] result = new Vector2[xl, yl];
//        for (int vi = y, j = 0; vi < y + yl; vi++, j++)
//        {
//            for (int hi = x, i = 0; hi < x + xl; hi++, i++)
//            {
//                result[i, j] = values[hi, vi];
//            }
//        }
//        return result;
//    }
//    private static byte[,] GetArea(byte[] values, int x, int y, int xl, int yl, int width, int height)
//    {
//        byte[,] result = new byte[xl, yl];
//        for (int vi = y, j = 0; j < yl; vi++, j++)
//        {
//            for (int hi = x, i = 0; i < xl; hi++, i++)
//            {
//                // currentI
//                var ci = vi * width + hi;
//                result[i, j] = values[ci];

//                if (result[i, j] > 0)
//                {

//                }
//            }
//        }
//        return result;
//    }

//    private static float GetNonZeroP90Value(Vector2[,] values)
//    {
//        float[] fvs = new float[values.Length * 2];

//        for (int vi = 0; vi < values.GetLength(1); vi++)
//        {
//            for (int hi = 0; hi < values.GetLength(0); hi++)
//            {
//                var pos = vi * values.GetLength(0) * 2 + vi * 2;
//                fvs[pos] = values[hi, vi].X;
//                fvs[pos + 1] = values[hi, vi].Y;
//            }
//        }

//        var nonZero = fvs.Where(n => n != 0).Select(Math.Abs).ToArray();
//        if (nonZero.Length == 0)
//        {
//            return 0;
//        }
//        var p = Helper.Percentile(nonZero, 0.9);
//        return (float)p;
//    }

//    //private static float GetMaxValue(Vector2[,] values)
//    //{
//    //    float max = 0;

//    //    for (int vi = 0; vi < values.GetLength(1); vi++)
//    //    {
//    //        for (int hi = 0; hi < values.GetLength(0); hi++)
//    //        {
//    //            if (values[hi, vi].X > max) max = values[hi, vi].X;
//    //            if (values[hi, vi].Y > max) max = values[hi, vi].Y;
//    //        }
//    //    }

//    //    return max;
//    //}
//    //private static float GetMaxValue(Vector2[,] values)
//    //{
//    //    float max = 0;

//    //    for (int vi = 0; vi < values.GetLength(1); vi++)
//    //    {
//    //        for (int hi = 0; hi < values.GetLength(0); hi++)
//    //        {
//    //            if (Math.Abs(values[hi, vi].X) > max) max = Math.Abs(values[hi, vi].X);
//    //            if (Math.Abs(values[hi, vi].Y) > max) max = Math.Abs(values[hi, vi].Y);
//    //        }
//    //    }

//    //    return max;
//    //}
//    //private static float GetAverageValue(Vector2[,] values)
//    //{
//    //    float sum = 0;
//    //    //float area = values.GetLength(1) * values.GetLength(0);

//    //    for (int vi = 0; vi < values.GetLength(1); vi++)
//    //    {
//    //        for (int hi = 0; hi < values.GetLength(0); hi++)
//    //        {
//    //            sum += Math.Abs(values[hi, vi].X) + Math.Abs(values[hi, vi].Y);
//    //        }
//    //    }

//    //    return sum;
//    //}

//    private static byte[,] GetPackedValues(byte[,] values, int horizontalSampleCount)
//    {
//        var samples = new byte[horizontalSampleCount, horizontalSampleCount];
//        var step = values.GetLength(0) / horizontalSampleCount;
//        for (int vi = 0, j = 0; j < horizontalSampleCount; vi += step, j++)
//        {
//            for (int hi = 0, i = 0; i < horizontalSampleCount; hi += step, i++)
//            {
//                samples[i, j] = values[hi, vi];
//            }
//        }
//        return samples;
//    }

//    public class PackedHeightmap
//    {
//        public Detail[] Details;
//        public int PixelHeight;
//        public int MapWidth;
//        public int MapHeight;
//        public int LineCount;
//    }
//    public class Detail
//    {
//        public byte[][][,] Lines;
//        public int EndOffset;
//        public int StartOffset;
//        public bool StartNewLine = false;
//        public bool HasMultipleLines => Lines.Length > 1;
//        public Vector2[] Coordinates;
//    }
//    public static async Task<PackedHeightmap> CreatePackedHeightMap(Map map)
//    {
//        try
//        {
//            const int samplesPerNode = 32;

//            var path = $"{Settings.OutputDirectory}/map_data/heightmap.png";
//            using var file = new MagickImageCollection(path);
//            var heightMap = file[0];

//            // the values go like this: grayscale, alpha. We only need grayscale.
//            var pixels = heightMap.GetPixels().GetValues().Where((n, i) => i % 2 == 0).ToArray();

//            var firstDerivative = Gradient(pixels, heightMap.Width, heightMap.Height);
//            var secondDerivative = Gradient(firstDerivative, heightMap.Width, heightMap.Height);

//            List<Vector2[,]> gradientAreas = [];
//            List<byte[,]> heightAreas = [];
//            List<Vector2> areaCoordinates = [];
//            for (int vi = 0; vi < secondDerivative.GetLength(1); vi += samplesPerNode)
//            {
//                for (int hi = 0; hi < secondDerivative.GetLength(0); hi += samplesPerNode)
//                {
//                    gradientAreas.Add(GetArea(secondDerivative, hi, vi, samplesPerNode, samplesPerNode));
//                    heightAreas.Add(GetArea(pixels, hi, vi, samplesPerNode, samplesPerNode, heightMap.Width, heightMap.Height));
//                    //areaCoordinates.Add(new Vector2(hi / samplesPerNode, vi / samplesPerNode));
//                    areaCoordinates.Add(new Vector2(hi, vi));
//                }
//            }

//            //var weightedDerivatives = gradientAreas.Select((n, i) => (i, heightArea: heightAreas[i], nonZeroP90: GetNonZeroP90Value(n), coordinates: areaCoordinates[i])).ToArray();
//            var weightedDerivatives = gradientAreas.Select((n, i) => (i, heightArea: heightAreas[i], nonZeroP90: GetNonZeroP90Value(n), coordinates: areaCoordinates[i])).ToArray();
//            //var detail = new[]
//            //{
//            //    weightedDerivatives.Where(n => n.nonZeroP90 >= 300).ToArray(),
//            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 200 and < 300).ToArray(),
//            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 100 and < 200).ToArray(),
//            //    weightedDerivatives.Where(n => n.nonZeroP90 is >= 50 and < 100).ToArray(),
//            //    weightedDerivatives.Where(n => n.nonZeroP90 < 50).ToArray(),
//            //};

//            var detail = new[]
//            {
//                weightedDerivatives.Where(n => n.nonZeroP90 >= 4).ToArray(),
//                weightedDerivatives.Where(n => n.nonZeroP90 is >= 3 and < 4).ToArray(),
//                weightedDerivatives.Where(n => n.nonZeroP90 is >= 2 and < 3).ToArray(),
//                weightedDerivatives.Where(n => n.nonZeroP90 is >= 1 and < 2).ToArray(),
//                weightedDerivatives.Where(n => n.nonZeroP90 < 1).ToArray(),
//            };

//            var detailSamples = detail.Select((d, i) => d.Select(n => GetPackedValues(n.heightArea, detailSize[i])).ToArray()).ToArray();

//            var dPerLine = detailSize.Select(n => packedWidth / n).ToArray();

//            var details = new Detail[detail.Length];
//            var maxDetailIndex = detail.Length - 1;
//            var packedHeightPixels = 0;

//            int? previousI = null;

//            int lineCount = 0;

//            for (var i = 0; i < details.Length; i++)
//            {
//                // Skip empty details
//                if (detailSamples[i].Length == 0)
//                {
//                    continue;
//                }

//                var d = details[i] = new Detail();
//                d.Coordinates = detail[i].Select(n => n.coordinates).ToArray();
//                // Largest detail has fewer checks
//                if (previousI == null)
//                {
//                    d.StartNewLine = true;
//                    d.Lines = detailSamples[i].Chunk(dPerLine[i]).ToArray();
//                    d.EndOffset = d.Lines.Last().Length * detailSize[i];
//                }
//                else
//                {
//                    d.StartOffset = details[previousI.Value].EndOffset % detailSize[i] > 0
//                       ? (details[previousI.Value].EndOffset + detailSize[i]) / detailSize[i] * detailSize[i]
//                       : details[previousI.Value].EndOffset;
//                    if (d.StartOffset > packedWidth - detailSize[i])
//                    {
//                        d.StartOffset = 0;
//                        d.StartNewLine = true;
//                    }
//                    d.Lines = d.StartNewLine
//                        ? detailSamples[i].Chunk(dPerLine[i]).ToArray()
//                        : (packedWidth - d.StartOffset) / detailSize[i] is var firstLineLength && firstLineLength < detailSamples[i].Length
//                        ? new[] { detailSamples[i].Take(firstLineLength).ToArray() }.Concat(detailSamples[i].Skip(firstLineLength).Chunk(dPerLine[i])).ToArray()
//                        : new[] { detailSamples[i] };
//                    d.EndOffset = d.HasMultipleLines
//                      ? d.Lines.Last().Length * detailSize[i]
//                      : d.StartOffset + d.Lines.Last().Length * detailSize[i];
//                }

//                if (d.StartNewLine)
//                {
//                    packedHeightPixels += d.Lines.Length * detailSize[i];
//                    lineCount += d.Lines.Length;
//                }
//                else
//                {
//                    if (d.HasMultipleLines)
//                    {
//                        packedHeightPixels += (d.Lines.Length - 1) * detailSize[i];
//                        lineCount += d.Lines.Length - 1;
//                    }
//                    else
//                    {
//                        // Starts and ends on the same line. Don't increase height.
//                    }
//                }

//                if (detailSamples[i].Length != 0)
//                {
//                    previousI = i;
//                }
//            }

//            return new PackedHeightmap
//            {
//                Details = details,
//                PixelHeight = packedHeightPixels,
//                MapWidth = heightMap.Width,
//                MapHeight = heightMap.Height,
//                LineCount = lineCount,
//            };
//        }
//        catch (Exception e)
//        {
//            Debugger.Break();
//            throw;
//        }
//    }
//    public static async Task WritePackedHeightMap(PackedHeightmap heightmap)
//    {
//        byte[] someNumbers = [1, 2, 4, 8, 16];

//        using var packed_heightmap = new MagickImage("xc:black", new MagickReadSettings()
//        {
//            Width = packedWidth,
//            Height = heightmap.PixelHeight,
//        });
//        var phDrawables = new Drawables();

//        Rgba32[] indirection_heightmap_pixelArray = new Rgba32[heightmap.MapWidth / indirectionProportion * heightmap.MapHeight / indirectionProportion];

//        int verticalOffset = 0;
//        int lineI = 0;

//        bool isWhite = false;

//        int[] levelOffsets = new int[5];

//        for (int di = 0; di < detailSize.Length; di++)
//        {
//            var d = heightmap.Details[di];
//            if (d is null) continue;

//            // coordinatesI
//            int ci = 0;
//            int rowI = 0;

//            for (int li = 0; li < d.Lines.Length; li++)
//            {
//                var line = d.Lines[li];
//                if (li == 0)
//                {
//                    if (d.StartNewLine)
//                    {
//                        levelOffsets[di] = verticalOffset;
//                    }
//                    else
//                    {
//                        levelOffsets[di] = verticalOffset - detailSize[di];
//                    }
//                }
//                if (d.StartNewLine || li > 0)
//                {
//                    verticalOffset += detailSize[di];
//                    lineI++;
//                }

//                for (int si = 0; si < line.Length; si++, ci++, rowI++)
//                {
//                    var sample = line[si];

//                    for (int sx = 0; sx < detailSize[di]; sx++)
//                    {
//                        for (int sy = 0; sy < detailSize[di]; sy++)
//                        {
//                            var c = sample[sx, sy];
//                            var phColor = new MagickColor(c, c, c);

//                            //phColor = isWhite ? new MagickColor(255, 255, 255) : new MagickColor(100, 100, 100);

//                            var phX = li == 0
//                                ? d.StartOffset + si * detailSize[di] + sx
//                                : si * detailSize[di] + sx;

//                            phDrawables
//                                .DisableStrokeAntialias()
//                                .StrokeColor(phColor)
//                                .FillColor(phColor)
//                                .Point(phX, heightmap.PixelHeight - verticalOffset + sy);
//                        }
//                    }

//                    //byte ihRowIndex = (byte)(packedWidth / detailSize[di] - (d.StartOffset / detailSize[di] + si));
//                    //byte ihRowIndex = (byte)((li == 0 ? d.StartOffset : 0) / detailSize[di] + si);
//                    byte ihRowIndex = (byte)(rowI);
//                    byte ihLineIndex = (byte)(heightmap.LineCount - lineI);
//                    //byte ihLineIndex = (byte)lineI;
//                    byte ihDetailSize = someNumbers[di];
//                    byte ihDetailI = (byte)di;
//                    var ihColor = new MagickColor(ihRowIndex, ihLineIndex, ihDetailSize, ihDetailI);

//                    isWhite = !isWhite;

//                    var ihXY = d.Coordinates[ci] / indirectionProportion;
//                    indirection_heightmap_pixelArray[(heightmap.MapWidth / indirectionProportion) * (int)ihXY.Y + (int)ihXY.X] = new Rgba32(ihRowIndex, ihLineIndex, ihDetailSize, ihDetailI);
//                }
//            }
//        }

//        packed_heightmap.Draw(phDrawables);
//        var phPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "packed_heightmap.png");
//        Directory.CreateDirectory(Path.GetDirectoryName(phPath));
//        await packed_heightmap.WriteAsync(phPath);

//        using var indirection_heightmap2 = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(indirection_heightmap_pixelArray, heightmap.MapWidth / indirectionProportion, heightmap.MapHeight / indirectionProportion);
//        var ihPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "indirection_heightmap.png");
//        Directory.CreateDirectory(Path.GetDirectoryName(ihPath));
//        indirection_heightmap2.Save(ihPath);

//        var heightmap_heightmap = $@"heightmap_file=""map_data/packed_heightmap.png""
//indirection_file=""map_data/indirection_heightmap.png""
//original_heightmap_size={{ {heightmap.MapWidth} {heightmap.MapHeight} }}
//tile_size=33
//should_wrap_x=no
//level_offsets={{ {string.Join(' ', levelOffsets.Select((n, i) => $"{{ 0 {n} }}"))} }} }}
//max_compress_level=4
//empty_tile_offset={{ 0 0 }}
//";
//        var hhPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "heightmap.heightmap");
//        Directory.CreateDirectory(Path.GetDirectoryName(hhPath));
//        await File.WriteAllTextAsync(hhPath, heightmap_heightmap);
//    }
//}