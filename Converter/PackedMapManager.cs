using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using static System.Net.Mime.MediaTypeNames;

namespace Converter;

public static class PackedMapManager
{
    private static readonly int[] detailSize = [31, 15, 9, 5, 3];
    //private const int packedWidth = 264;
    private const int packedWidth = 768;
    private const int indirectionProportion = 32;

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
        public int MapWidth;
        public int MapHeight;
        public int LineCount;
    }
    public class Detail
    {
        public byte[][][,] Lines;
        public int EndOffset;
        public int StartOffset;
        public bool StartNewLine = false;
        public bool HasMultipleLines => Lines.Length > 0;
        public Vector2[] Coordinates;
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

            var firstDerivative = Gradient(pixels, heightMap.Width, heightMap.Height);
            var secondDerivative = Gradient(firstDerivative, heightMap.Width, heightMap.Height);

            List<Vector2[,]> gradientAreas = [];
            List<byte[,]> heightAreas = [];
            List<Vector2> areaCoordinates = [];
            for (int vi = 0; vi < secondDerivative.GetLength(1); vi += samplesPerNode)
            {
                for (int hi = 0; hi < secondDerivative.GetLength(0); hi += samplesPerNode)
                {
                    gradientAreas.Add(GetArea(secondDerivative, hi, vi, samplesPerNode, samplesPerNode));
                    heightAreas.Add(GetArea(pixels, hi, vi, samplesPerNode, samplesPerNode, heightMap.Width, heightMap.Height));
                    //areaCoordinates.Add(new Vector2(hi / samplesPerNode, vi / samplesPerNode));
                    areaCoordinates.Add(new Vector2(hi, vi));
                }
            }

            var weightedDerivatives = gradientAreas.Select((n, i) => (i, heightArea: heightAreas[i], nonZeroP90: GetNonZeroP90Value(n), coordinates: areaCoordinates[i])).ToArray();
            var detail = new[]
            {
                weightedDerivatives.Where(n => n.nonZeroP90 >= 300).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 200 and < 300).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 100 and < 200).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 is >= 50 and < 100).ToArray(),
                weightedDerivatives.Where(n => n.nonZeroP90 < 50).ToArray(),
            };
            var detailSamples = detail.Select((d, i) => d.Select(n => GetPackedValues(n.heightArea, detailSize[i])).ToArray()).ToArray();
            
            var dPerLine = detailSize.Select(n => packedWidth / n).ToArray();

            var details = new Detail[detail.Length];
            var maxDetailIndex = detail.Length - 1;
            var packedHeightPixels = 0;

            int? previousI = null;

            int lineCount = 0;

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
                    lineCount += d.Lines.Length;
                }
                else
                {
                    if (d.HasMultipleLines)
                    {
                        packedHeightPixels += (d.Lines.Length - 1) * detailSize[i];
                        lineCount += d.Lines.Length - 1;
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
                MapWidth = heightMap.Width,
                MapHeight = heightMap.Height,
                LineCount = lineCount,
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
        //using var heightmap = new MagickImage(Helper.GetPath(SettingsManager.ExecutablePath, "indirection_heightmap_template.png"));

        //using var indirection_heightmap = new MagickImage("xc:black", new MagickReadSettings()
        using var indirection_heightmap = new MagickImage(Helper.GetPath(SettingsManager.ExecutablePath, "indirection_heightmap_template.png"));
        var ihDrawables = new Drawables();

        Rgba32[] indirection_heightmap_pixelArray = new Rgba32[heightmap.MapWidth / indirectionProportion * heightmap.MapHeight / indirectionProportion];

        int verticalOffset = 0;
        int lineI = 0;

        for (int di = 0; di < detailSize.Length; di++)
        {
            var d = heightmap.Details[di];
            if (d is null) continue;

            // coordinatesI
            int ci = 0;

            for (int li = 0; li < d.Lines.Length; li++)
            {
                var line = d.Lines[li];
                if (d.StartNewLine || li > 0)
                {
                    verticalOffset += detailSize[di];
                    lineI++;
                }

                for (int si = 0; si < line.Length; si++, ci++)
                {
                    var sample = line[si];

                    for (int sx = 0; sx < detailSize[di]; sx++)
                    {
                        for (int sy = 0; sy < detailSize[di]; sy++)
                        {
                            var c = sample[sx, sy];
                            var phColor = new MagickColor(c, c, c);
                            var phX = li == 0
                                ? d.StartOffset + si * detailSize[di] + sx
                                : si * detailSize[di] + sx;

                            phDrawables
                                .DisableStrokeAntialias()
                                .StrokeColor(phColor)
                                .FillColor(phColor)
                                .Point(phX, heightmap.PixelHeight - verticalOffset + sy);
                        }
                    }

                    //byte ihRowIndex = (byte)(packedWidth / detailSize[di] - (d.StartOffset / detailSize[di] + si));
                    byte ihRowIndex = (byte)((li == 0 ? d.StartOffset : 0) / detailSize[di] + si);
                    //byte ihLineIndex = (byte)(heightmap.LineCount - lineI - 1);
                    byte ihLineIndex = (byte)(heightmap.LineCount - lineI);
                    //byte ihLineIndex = (byte)lineI;
                    byte ihDetailSize = someNumbers[di];
                    byte ihDetailI = (byte)di;
                    var ihColor = new MagickColor(ihRowIndex, ihLineIndex, ihDetailSize, ihDetailI);

                    if (ihLineIndex > 128)
                    {

                    }
                    var ihXY = d.Coordinates[ci] / indirectionProportion;
                    indirection_heightmap_pixelArray[(heightmap.MapWidth / indirectionProportion) * (int)ihXY.Y + (int)ihXY.X] = new Rgba32(ihRowIndex, ihLineIndex, ihDetailSize, ihDetailI);
                    ihDrawables
                        .DisableStrokeAntialias()
                        .FillColor(ihColor)
                        .Point((int)ihXY.X, (int)ihXY.Y);
                }
            }
        }

        packed_heightmap.Draw(phDrawables);
        var phPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "packed_heightmap.png");
        Directory.CreateDirectory(Path.GetDirectoryName(phPath));
        await packed_heightmap.WriteAsync(phPath);


        //indirection_heightmap.Draw(ihDrawables);
        var ihPath = Helper.GetPath(Settings.OutputDirectory, "map_data", "indirection_heightmap.png");
        Directory.CreateDirectory(Path.GetDirectoryName(ihPath));
        //await indirection_heightmap.WriteAsync(ihPath);

        using var ss1 = new MagickImage(Helper.GetPath(SettingsManager.ExecutablePath, "indirection_heightmap_template.png"));
        using var ss = new MagickImage(Helper.GetPath(Settings.OutputDirectory, "map_data", "indirection_heightmap.png"));


        using var indirection_heightmap2 = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(indirection_heightmap_pixelArray, heightmap.MapWidth / indirectionProportion, heightmap.MapHeight / indirectionProportion);
        indirection_heightmap2.Save(ihPath);
    }
}
