using ImageMagick;
using SixLabors.ImageSharp;
using System.Diagnostics;
using System.Numerics;

namespace Converter;

public static class PackedMapManager
{

    private static Vector2[,] Gradient(byte[] values, int width, int height)
    {
        Vector2[,] result = new Vector2[width, height];
        for (int hi = 1; hi < width; hi++)
        {
            for (int vi = 1; vi < height; vi++)
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
        for (int hi = 1; hi < width; hi++)
        {
            for (int vi = 1; vi < height; vi++)
            {
                var h = values[hi, vi].X - values[hi - 1, vi].X;
                var v = values[hi, vi].Y - values[hi, vi - 1].Y;

                if (values[hi, vi].X > 1 || values[hi, vi].Y > 1)
                {

                }
                result[hi, vi] = new Vector2(h, v);
            }
        }
        return result;
    }
    private static Vector2[,] GetArea(Vector2[,] values, int x, int y, int xl, int yl)
    {
        Vector2[,] result = new Vector2[xl, yl];
        for (int hi = x, i = 0; hi < x + xl; hi++, i++)
        {
            for (int vi = y, j = 0; vi < y + yl; vi++, j++)
            {
                result[i, j] = values[hi, vi];
                if (result[i, j].X > 0 || result[i, j].Y > 0)
                {

                }
            }
        }
        return result;
    }
    private static byte[,] GetArea(byte[] values, int x, int y, int xl, int yl, int width, int height)
    {
        byte[,] result = new byte[xl, yl];
        for (int hi = x, i = 0; i < xl; hi++, i++)
        {
            for (int vi = y, j = 0; j < yl; vi++, j++)
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

    private static float GetMaxValue(Vector2[,] values)
    {
        float max = 0;

        for (int hi = 0; hi < values.GetLength(0); hi++)
        {
            for (int vi = 0; vi < values.GetLength(1); vi++)
            {
                if (values[hi, vi].X > max)
                {
                    max = values[hi, vi].X;
                }
                else if (values[hi, vi].Y > max)
                {
                    max = values[hi, vi].Y;
                }
            }
        }

        return max;
    }
    private static float GetNonZeroP90Value(Vector2[,] values)
    {
        float[] fvs = new float[values.Length * 2];
        float avg = 0;

        for (int hi = 0; hi < values.GetLength(0); hi++)
        {
            for (int vi = 0; vi < values.GetLength(1); vi++)
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
        for (int hi = 0, i = 0; i < horizontalSampleCount; hi += step, i++)
        {
            for (int vi = 0, j = 0; j < horizontalSampleCount; vi += step, j++)
            {
                samples[i, j] = values[hi, vi];
                if (values[hi, vi] > 0)
                {

                }
            }
        }
        return samples;
    }

    public static async Task CreatePackedHeightMap(Map map)
    {
        const int samplesPerNode = 32;

        var path = $"{Settings.OutputDirectory}/map_data/heightmap.png";
        using var file = new MagickImageCollection(path);
        var heightMap = file[0];
        var pixels = heightMap.GetPixels().GetValues();

        var s = Stopwatch.StartNew();

        var firstDerivative = Gradient(pixels, heightMap.Width, heightMap.Height);
        var secondDerivative = Gradient(firstDerivative, heightMap.Width, heightMap.Height);

        var t = s.Elapsed.Milliseconds;

        List<Vector2[,]> gradientAreas = [];
        List<byte[,]> heightAreas = [];
        for (int hi = 0; hi < secondDerivative.GetLength(0); hi += samplesPerNode)
        {
            for (int vi = 0; vi < secondDerivative.GetLength(1); vi += samplesPerNode)
            {
                gradientAreas.Add(GetArea(secondDerivative, hi, vi, samplesPerNode, samplesPerNode));
                heightAreas.Add(GetArea(pixels, hi, vi, samplesPerNode, samplesPerNode, heightMap.Width, heightMap.Height));
            }
        }

        var t1 = s.Elapsed.Milliseconds;

        var weightedDerivatives = gradientAreas.Select((n, i) => (i, gradientArea: n, heightArea: heightAreas[i], nonZeroP90: GetNonZeroP90Value(n))).ToArray();
        var t2 = s.Elapsed.Milliseconds;

        var detail5 = weightedDerivatives.Where(n => n.nonZeroP90 >= 300).ToArray();
        var detail4 = weightedDerivatives.Where(n => n.nonZeroP90 is >= 200 and < 300).ToArray();
        var detail3 = weightedDerivatives.Where(n => n.nonZeroP90 is >= 100 and < 200).ToArray();
        var detail2 = weightedDerivatives.Where(n => n.nonZeroP90 is >= 50 and < 100).ToArray();
        var detail1 = weightedDerivatives.Where(n => n.nonZeroP90 < 50).ToArray();

        var detail5Samples = detail5.Select(n => GetPackedValues(n.heightArea, 31)).ToArray();
        var detail4Samples = detail4.Select(n => GetPackedValues(n.heightArea, 15)).ToArray();
        var detail3Samples = detail3.Select(n => GetPackedValues(n.heightArea, 9)).ToArray();
        var detail2Samples = detail2.Select(n => GetPackedValues(n.heightArea, 5)).ToArray();
        var detail1Samples = detail1.Select(n => GetPackedValues(n.heightArea, 3)).ToArray();

        var t3 = s.Elapsed.Milliseconds;

        var i = 0;

        var packedWidth = 264;




    }
}
