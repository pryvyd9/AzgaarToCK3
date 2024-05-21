using ImageMagick;
using SixLabors.ImageSharp;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;

namespace Converter;

public static class PackedMapManager
{

    private static Vector2[,] Gradient(byte[] values, int width, int height)
    {
        Vector2[,] result = new Vector2[width, height];
        for (int vi = 1; vi < height; vi++)
        {
            for (int hi = 1; hi < width; hi++)
            {
                // currentI
                var ci = vi * width + hi;
                // leftI
                var li = ci - 1;
                // upI
                var up = ci - width;

                var h = values[ci] - values[li];
                var v = values[ci] - values[up];
                result[hi, vi] = new Vector2(h, v);
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
    private static byte[,] GetArea(byte[] values, int x, int y, int xl, int yl, int width, int height)
    {
        byte[,] result = new byte[xl, yl];
        for (int vi = y, j = 0; j < yl; vi++, j++)
        {
            for (int hi = x, i = 0; i < xl; hi++, i++)
            {
                // currentI
                var ci = vi * width + hi;
                result[i, j] = values[ci];

                if (result[i, j] > 0)
                {

                }
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

    private static byte[,] GetPackedValues(byte[,] values, int horizontalSampleCount)
    {
        var samples = new byte[horizontalSampleCount, horizontalSampleCount];
        var step = values.GetLength(0) / horizontalSampleCount;
        for (int vi = 0, j = 0; j < horizontalSampleCount; vi += step, j++)
        {
            for (int hi = 0, i = 0; i < horizontalSampleCount; hi += step, i++)
            {
                samples[i, j] = values[hi, vi];
            }
        }
        return samples;
    }

    public class PackedHeightmap
    {
        public Detail[] Details;
        public int PixelHeight;
    }
    public class Detail
    {
        public byte[][][,] Lines;
        public int EndOffset;
        public int StartOffset;
        public bool StartNewLine = false;
        public bool HasMultipleLines => Lines.Length > 0;

    }
    public static async Task<PackedHeightmap> CreatePackedHeightMap(Map map)
    {
        try
        {
            const int samplesPerNode = 32;

            var path = $"{Settings.OutputDirectory}/map_data/heightmap.png";
            using var file = new MagickImageCollection(path);
            var heightMap = file[0];

            // the values go like this: grayscale, alpha. We only need grayscale.
            var pixels = heightMap.GetPixels().GetValues().Where((n, i) => i % 2 == 0).ToArray();
            var s = Stopwatch.StartNew();

            var firstDerivative = Gradient(pixels, heightMap.Width, heightMap.Height);
            var secondDerivative = Gradient(firstDerivative, heightMap.Width, heightMap.Height);

            List<Vector2[,]> gradientAreas = [];
            List<byte[,]> heightAreas = [];
            for (int vi = 0; vi < secondDerivative.GetLength(1); vi += samplesPerNode)
            {
                for (int hi = 0; hi < secondDerivative.GetLength(0); hi += samplesPerNode)
                {
                    gradientAreas.Add(GetArea(secondDerivative, hi, vi, samplesPerNode, samplesPerNode));
                    heightAreas.Add(GetArea(pixels, hi, vi, samplesPerNode, samplesPerNode, heightMap.Width, heightMap.Height));
                }
            }

            var weightedDerivatives = gradientAreas.Select((n, i) => (i, heightArea: heightAreas[i], nonZeroP90: GetNonZeroP90Value(n))).ToArray();
            var detail = new[]
            {
                weightedDerivatives.Where(n => n.nonZeroP90 < 50).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 50 and < 100).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 100 and < 200).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 200 and < 300).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 >= 300).ToArray(),
            };
            var detailSize = new[] { 3, 5, 9, 15, 31 };
            var detailSamples = detail.Select((d, i) => d.Select(n => GetPackedValues(n.heightArea, detailSize[i])).ToArray()).ToArray();

            const int packedWidth = 264;
            var dPerLine = detailSize.Select(n => packedWidth / n).ToArray();

            var details = new Detail[detail.Length];
            var maxDetailIndex = detail.Length - 1;
            var packedHeightPixels = 0;

            int? previousI = null;

            for (var i = maxDetailIndex; i >= 0; i--)
            {
                // Skip empty details
                if (detailSamples[i].Length == 0)
                {
                    continue;
                }
               
                var d = details[i] = new Detail();

                // Largest detail has fewer checks
                if (previousI == null)
                {
                    d.StartNewLine = true;
                    d.Lines = detailSamples[i].Chunk(dPerLine[i]).ToArray();
                    d.EndOffset = d.Lines.Last().Length * detailSize[i];
                }
                else
                {
                    d.StartOffset = details[previousI.Value].EndOffset % detailSize[i] > 0
                       ? details[previousI.Value].EndOffset / detailSize[i] * (detailSize[i] + 1)
                       : details[previousI.Value].EndOffset;
                    if (d.StartOffset > packedWidth - detailSize[i])
                    {
                        d.StartOffset = 0;
                        d.StartNewLine = true;
                    }
                    d.Lines = d.StartNewLine
                        ? detailSamples[i].Chunk(dPerLine[i]).ToArray()
                        : (packedWidth - d.StartOffset) / detailSize[i] is var firstLineLength && firstLineLength < detailSamples[i].Length
                        ? new[] { detailSamples[i].Take(firstLineLength).ToArray() }.Concat(detailSamples[i].Skip(firstLineLength).Chunk(dPerLine[i])).ToArray()
                        : new[] { detailSamples[i] };
                    d.EndOffset = d.HasMultipleLines
                      ? d.Lines.Last().Length
                      : d.StartOffset + d.Lines.Last().Length;
                }

                if (d.StartNewLine)
                {
                    packedHeightPixels += d.Lines.Length * detailSize[i];
                }
                else
                {
                    if (d.HasMultipleLines)
                    {
                        packedHeightPixels += (d.Lines.Length - 1) * detailSize[i];
                    }
                    else
                    {
                        // Starts and ends on the same line. Don't increase height.
                    }
                }

                if (detailSamples[i].Length != 0)
                {
                    previousI = i;
                }
            }

            return new PackedHeightmap
            {
                Details = details,
                PixelHeight = packedHeightPixels,
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
        var detailSize = new[] { 3, 5, 9, 15, 31 };
        const int packedWidth = 264;

        var settings = new MagickReadSettings()
        {
            Width = packedWidth,
            Height = heightmap.PixelHeight,
        };
        using var image = new MagickImage("xc:black", settings);
        var drawables = new Drawables();

        int verticalOffset = 0;

        for (int di = detailSize.Length - 1; di >= 0; di--)
        {
            var d = heightmap.Details[di];
            if (d is null) continue;

            for (int li = 0; li < d.Lines.Length; li++)
            {
                var line = d.Lines[li];

                if (d.StartNewLine || li > 0)
                {
                    verticalOffset += detailSize[di];
                }

                for (int si = 0; si < line.Length; si++)
                {
                    var sample = line[si];

                    for (int sx = 0; sx < detailSize[di]; sx++)
                    {
                        for (int sy = 0; sy < detailSize[di]; sy++)
                        {
                            var c = sample[sx, sy];
                            var color = new MagickColor(c, c, c);
                            var x = li == 0
                                ? d.StartOffset + si * detailSize[di] + sx
                                : si * detailSize[di] + sx;

                            drawables
                                .DisableStrokeAntialias()
                                .StrokeColor(color)
                                .FillColor(color)
                                .Point(x, heightmap.PixelHeight - verticalOffset + sy);
                        }
                    }
                }
            }
        }

        image.Draw(drawables);
        var path = Helper.GetPath(Settings.OutputDirectory, "map_data", "packed_heightmap.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await image.WriteAsync(path);
    }
}
