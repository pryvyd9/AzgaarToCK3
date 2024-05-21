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
                var ci = hi * width + vi;
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
                var ci = hi * width + vi;
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

    //public static async Task CreatePackedHeightMap(Map map)
    //{
    //    const int samplesPerNode = 32;

    //    var path = $"{Settings.OutputDirectory}/map_data/heightmap.png";
    //    using var file = new MagickImageCollection(path);
    //    var heightMap = file[0];
    //    var pixels = heightMap.GetPixels().GetValues();

    //    var s = Stopwatch.StartNew();

    //    var firstDerivative = Gradient(pixels, heightMap.Width, heightMap.Height);
    //    var secondDerivative = Gradient(firstDerivative, heightMap.Width, heightMap.Height);

    //    List<Vector2[,]> gradientAreas = [];
    //    List<byte[,]> heightAreas = [];
    //    for (int vi = 0; vi < secondDerivative.GetLength(1); vi += samplesPerNode)
    //    {
    //        for (int hi = 0; hi < secondDerivative.GetLength(0); hi += samplesPerNode)
    //        {
    //            gradientAreas.Add(GetArea(secondDerivative, hi, vi, samplesPerNode, samplesPerNode));
    //            heightAreas.Add(GetArea(pixels, hi, vi, samplesPerNode, samplesPerNode, heightMap.Width, heightMap.Height));
    //        }
    //    }

    //    var weightedDerivatives = gradientAreas.Select((n, i) => (i, gradientArea: n, heightArea: heightAreas[i], nonZeroP90: GetNonZeroP90Value(n))).ToArray();

    //    var detail5 = weightedDerivatives.Where(n => n.nonZeroP90 >= 300).ToArray();
    //    var detail4 = weightedDerivatives.Where(n => n.nonZeroP90 is >= 200 and < 300).ToArray();
    //    var detail3 = weightedDerivatives.Where(n => n.nonZeroP90 is >= 100 and < 200).ToArray();
    //    var detail2 = weightedDerivatives.Where(n => n.nonZeroP90 is >= 50 and < 100).ToArray();
    //    var detail1 = weightedDerivatives.Where(n => n.nonZeroP90 < 50).ToArray();

    //    const int detail1Size = 3;
    //    const int detail2Size = 5;
    //    const int detail3Size = 9;
    //    const int detail4Size = 15;
    //    const int detail5Size = 31;

    //    var detail1Samples = detail1.Select(n => GetPackedValues(n.heightArea, detail1Size)).ToArray();
    //    var detail2Samples = detail2.Select(n => GetPackedValues(n.heightArea, detail2Size)).ToArray();
    //    var detail3Samples = detail3.Select(n => GetPackedValues(n.heightArea, detail3Size)).ToArray();
    //    var detail4Samples = detail4.Select(n => GetPackedValues(n.heightArea, detail4Size)).ToArray();
    //    var detail5Samples = detail5.Select(n => GetPackedValues(n.heightArea, detail5Size)).ToArray();

    //    const int packedWidth = 264;
    //    const int detail1PerLine = packedWidth / detail1Size;
    //    const int detail2PerLine = packedWidth / detail2Size;
    //    const int detail3PerLine = packedWidth / detail3Size;
    //    const int detail4PerLine = packedWidth / detail4Size;
    //    const int detail5PerLine = packedWidth / detail5Size;

    //    const int detail1RightOffset = packedWidth - detail1PerLine * detail1Size;
    //    const int detail2RightOffset = packedWidth - detail2PerLine * detail2Size;
    //    const int detail3RightOffset = packedWidth - detail3PerLine * detail3Size;
    //    const int detail4RightOffset = packedWidth - detail4PerLine * detail4Size;
    //    const int detail5RightOffset = packedWidth - detail5PerLine * detail5Size;

    //    // Samples are written right to left with initial right offset.
    //    var detail1Lines = detail1Samples.Chunk(detail1PerLine).ToArray();
    //    var detail1LinesReversed = detail1Lines.Select(n => n.Reverse()).ToArray();
    //    var detail1EndRightOffset = detail1RightOffset + detail1Lines.Last().Length * detail1Size;

    //    // / detail2Size * detail2Size is needed to drop unaligned pixels
    //    var detail2StartRightOffset = detail1Lines.Last().Length == detail1PerLine
    //        ? detail2RightOffset
    //        : (packedWidth - detail1EndRightOffset) / detail2Size * detail2Size;
    //    var detail2FirstRowCount = (packedWidth - detail2StartRightOffset) / detail2Size;
    //    var detail2Lines = detail2FirstRowCount > detail2Samples.Length
    //        ? new[] { detail2Samples.Take(detail2FirstRowCount).ToArray() }.Concat(detail2Samples.Skip(detail2FirstRowCount).Chunk(detail2PerLine)).ToArray()
    //        : new[] { detail2Samples.Take(detail2FirstRowCount).ToArray() };
    //    var detail2LinesReversed = detail2Lines.Select(n => n.Reverse()).ToArray();
    //    var detail2EndsSameLine = detail2Lines.Length == 1;
    //    var detail2EndRightOffset = detail2EndsSameLine
    //        ? detail2StartRightOffset + detail2Samples.Length * detail2Size
    //        : detail1RightOffset + detail2Lines.Last().Length * detail2Size;

    //    var detail3StartRightOffset = detail2EndRightOffset == packedWidth
    //      ? detail3RightOffset
    //      : (packedWidth - detail2EndRightOffset) / detail3Size * detail3Size;
    //    var detail3FirstRowCount = (packedWidth - detail3StartRightOffset) / detail3Size;
    //    var detail3Lines = detail3FirstRowCount > detail3Samples.Length
    //        ? new[] { detail3Samples.Take(detail3FirstRowCount).ToArray() }.Concat(detail3Samples.Skip(detail3FirstRowCount).Chunk(detail3PerLine)).ToArray()
    //        : new[] { detail3Samples.Take(detail3FirstRowCount).ToArray() };
    //    var detail3LinesReversed = detail3Lines.Select(n => n.Reverse()).ToArray();
    //    var detail3EndsSameLine = detail3Lines.Length == 1;
    //    var detail3EndRightOffset = detail3EndsSameLine
    //      ? detail3StartRightOffset + detail3Samples.Length * detail3Size
    //      : detail2RightOffset + detail3Lines.Last().Length * detail3Size;


    //}
    //public static async Task CreatePackedHeightMap(Map map)
    //{
    //    const int samplesPerNode = 32;

    //    var path = $"{Settings.OutputDirectory}/map_data/heightmap.png";
    //    using var file = new MagickImageCollection(path);
    //    var heightMap = file[0];
    //    var pixels = heightMap.GetPixels().GetValues();

    //    var s = Stopwatch.StartNew();

    //    var firstDerivative = Gradient(pixels, heightMap.Width, heightMap.Height);
    //    var secondDerivative = Gradient(firstDerivative, heightMap.Width, heightMap.Height);

    //    List<Vector2[,]> gradientAreas = [];
    //    List<byte[,]> heightAreas = [];
    //    for (int vi = 0; vi < secondDerivative.GetLength(1); vi += samplesPerNode)
    //    {
    //        for (int hi = 0; hi < secondDerivative.GetLength(0); hi += samplesPerNode)
    //        {
    //            gradientAreas.Add(GetArea(secondDerivative, hi, vi, samplesPerNode, samplesPerNode));
    //            heightAreas.Add(GetArea(pixels, hi, vi, samplesPerNode, samplesPerNode, heightMap.Width, heightMap.Height));
    //        }
    //    }

    //    var weightedDerivatives = gradientAreas.Select((n, i) => (i, gradientArea: n, heightArea: heightAreas[i], nonZeroP90: GetNonZeroP90Value(n))).ToArray();

    //    var detail5 = weightedDerivatives.Where(n => n.nonZeroP90 >= 300).ToArray();
    //    var detail4 = weightedDerivatives.Where(n => n.nonZeroP90 is >= 200 and < 300).ToArray();
    //    var detail3 = weightedDerivatives.Where(n => n.nonZeroP90 is >= 100 and < 200).ToArray();
    //    var detail2 = weightedDerivatives.Where(n => n.nonZeroP90 is >= 50 and < 100).ToArray();
    //    var detail1 = weightedDerivatives.Where(n => n.nonZeroP90 < 50).ToArray();

    //    const int detail5Size = 31;
    //    const int detail4Size = 15;
    //    const int detail3Size = 9;
    //    const int detail2Size = 5;
    //    const int detail1Size = 3;

    //    var detail5Samples = detail5.Select(n => GetPackedValues(n.heightArea, detail5Size)).ToArray();
    //    var detail4Samples = detail4.Select(n => GetPackedValues(n.heightArea, detail4Size)).ToArray();
    //    var detail3Samples = detail3.Select(n => GetPackedValues(n.heightArea, detail3Size)).ToArray();
    //    var detail2Samples = detail2.Select(n => GetPackedValues(n.heightArea, detail2Size)).ToArray();
    //    var detail1Samples = detail1.Select(n => GetPackedValues(n.heightArea, detail1Size)).ToArray();

    //    const int packedWidth = 264;
    //    const int d5PerLine = packedWidth / detail5Size;
    //    const int d4PerLine = packedWidth / detail4Size;
    //    const int d3PerLine = packedWidth / detail3Size;
    //    const int d2PerLine = packedWidth / detail2Size;
    //    const int d1PerLine = packedWidth / detail1Size;

    //    //const int d5RightOffset = packedWidth - d5PerLine * detail5Size;
    //    //const int d4RightOffset = packedWidth - d4PerLine * detail4Size;
    //    //const int d3RightOffset = packedWidth - d3PerLine * detail3Size;
    //    //const int d2RightOffset = packedWidth - d2PerLine * detail2Size;
    //    //const int d1RightOffset = packedWidth - d1PerLine * detail1Size;

    //    // Samples are written bottom up, left to right.
    //    var d5Lines = detail5Samples.Chunk(d5PerLine).ToArray();
    //    var d5EndOffset = d5Lines.Last().Length * detail5Size;

    //    var d4StartOffset = d5EndOffset % detail4Size > 0
    //        ? d5EndOffset / detail4Size * (detail4Size + 1)
    //        : d5EndOffset;
    //    var d4StartNewLine = false;
    //    if (d4StartOffset > packedWidth - detail4Size)
    //    {
    //        d4StartOffset = 0;
    //        d4StartNewLine = true;
    //    }
    //    var d4HasMultipleLines = ((packedWidth - d4StartOffset) / detail4Size) > detail4Samples.Length;
    //    var d4Lines = d4StartNewLine
    //        ? detail4Samples.Chunk(d4PerLine).ToArray()
    //        : (packedWidth - d4StartOffset) / detail4Size * detail4Size is var d4FirstLineLength && d4FirstLineLength < detail4Samples.Length
    //        ? new[] { detail4Samples.Take(d4FirstLineLength).ToArray() }.Concat(detail4Samples.Skip(d4FirstLineLength).Chunk(d4PerLine)).ToArray()
    //        : new[] { detail4Samples };
    //    var d4EndOffset = d4HasMultipleLines
    //        ? d4Lines.Last().Length
    //        : d4StartOffset + d4Lines.Last().Length;

    //    var d3StartOffset = d4EndOffset % detail3Size > 0
    //        ? d4EndOffset / detail3Size * (detail3Size + 1)
    //        : d4EndOffset;
    //    var d3StartNewLine = false;
    //    if (d3StartOffset > packedWidth - detail3Size)
    //    {
    //        d3StartOffset = 0;
    //        d3StartNewLine = true;
    //    }
    //    var d3HasMultipleLines = ((packedWidth - d3StartOffset) / detail3Size) > detail3Samples.Length;
    //    var d3Lines = d3StartNewLine
    //        ? detail3Samples.Chunk(d3PerLine).ToArray()
    //        : (packedWidth - d3StartOffset) / detail3Size * detail3Size is var d3FirstLineLength && d3FirstLineLength < detail3Samples.Length
    //        ? new[] { detail3Samples.Take(d3FirstLineLength).ToArray() }.Concat(detail3Samples.Skip(d3FirstLineLength).Chunk(d3PerLine)).ToArray()
    //        : new[] { detail3Samples };
    //    var d3EndOffset = d3HasMultipleLines
    //        ? d3Lines.Last().Length
    //        : d3StartOffset + d3Lines.Last().Length;

    //    var d2StartOffset = d3EndOffset % detail2Size > 0
    //       ? d3EndOffset / detail2Size * (detail2Size + 1)
    //       : d3EndOffset;
    //    var d2StartNewLine = false;
    //    if (d2StartOffset > packedWidth - detail2Size)
    //    {
    //        d2StartOffset = 0;
    //        d2StartNewLine = true;
    //    }
    //    var d2HasMultipleLines = ((packedWidth - d2StartOffset) / detail2Size) > detail2Samples.Length;
    //    var d2Lines = d2StartNewLine
    //       ? detail2Samples.Chunk(d2PerLine).ToArray()
    //       : (packedWidth - d2StartOffset) / detail2Size * detail2Size is var d2FirstLineLength && d2FirstLineLength < detail2Samples.Length
    //       ? new[] { detail2Samples.Take(d2FirstLineLength).ToArray() }.Concat(detail2Samples.Skip(d2FirstLineLength).Chunk(d2PerLine)).ToArray()
    //       : new[] { detail2Samples };
    //    var d2EndOffset = d2HasMultipleLines
    //        ? d2Lines.Last().Length
    //        : d2StartOffset + d2Lines.Last().Length;

    //    var d1StartOffset = d2EndOffset % detail1Size > 0
    //     ? d2EndOffset / detail1Size * (detail1Size + 1)
    //     : d2EndOffset;
    //    var d1StartNewLine = false;
    //    if (d1StartOffset > packedWidth - detail1Size)
    //    {
    //        d1StartOffset = 0;
    //        d1StartNewLine = true;
    //    }
    //    var d1HasMultipleLines = ((packedWidth - d1StartOffset) / detail1Size) > detail1Samples.Length;
    //    var d1Lines = d1StartNewLine
    //       ? detail1Samples.Chunk(d1PerLine).ToArray()
    //       : (packedWidth - d1StartOffset) / detail1Size * detail1Size is var d1FirstLineLength && d1FirstLineLength < detail1Samples.Length
    //       ? new[] { detail1Samples.Take(d1FirstLineLength).ToArray() }.Concat(detail1Samples.Skip(d1FirstLineLength).Chunk(d1PerLine)).ToArray()
    //       : new[] { detail1Samples };
    //    var d1EndOffset = d1HasMultipleLines
    //        ? d1Lines.Last().Length
    //        : d1StartOffset + d1Lines.Last().Length;

    //}

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
            var pixels = heightMap.GetPixels().GetValues();

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

            var weightedDerivatives = gradientAreas.Select((n, i) => (i, gradientArea: n, heightArea: heightAreas[i], nonZeroP90: GetNonZeroP90Value(n))).ToArray();
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
                    //d.HasMultipleLines = d.Lines.Length > 0;
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
            if (heightmap.Details[di] is null) continue;

            for (int li = 0; li < d.Lines.Length; li++)
            {
                var line = d.Lines[li];

                for (int si = 0; si < line.Length; si++)
                {
                    var sample = line[si];
                    if (d.StartNewLine)
                    {
                        verticalOffset += detailSize[di];
                    }

                    for (int sx = 0; sx < detailSize[di]; sx++)
                    {
                        for (int sy = 0; sy < detailSize[di]; sy++)
                        {
                            var c = sample[sx, sy];
                            var color = new MagickColor(c, c, c);
                            drawables
                                .DisableStrokeAntialias()
                                .StrokeColor(color)
                                .FillColor(color)
                                .Point(d.StartOffset + si * detailSize[di] + sx, heightmap.PixelHeight - verticalOffset + sy);
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
