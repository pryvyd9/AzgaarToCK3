using ImageMagick;

namespace Converter.Lemur;

public static class Helper
{
    public static PointD GeoToPixel(float lon, float lat, Entities.Map map)
    {
        return new PointD((lon - map.XOffset) * map.XRatio, Map.MapHeight - (lat - map.YOffset) * map.YRatio);
    }

    public static string GetPath(params string[] paths)
    {
        if (paths is null) return "";
        return Path.Combine(paths.SelectMany(n => n.Replace(@"\\", "/").Replace(@"\", "/").Split("/")).ToArray());
    }

    public static void PrintSectionHeader(string header)
    {
        string line = new('=', header.Length);
        Console.WriteLine(line);
        Console.WriteLine(header);
        Console.WriteLine(line);
    }
    /// <summary>
    /// Generate a unique color for i along the range of 0 to maxI.
    /// </summary>
    /// <param name="i"> Must be less than maxI</param>
    /// <param name="maxI"></param>
    /// <returns></returns>
    /// <exception cref="FormatException"></exception>
    public static MagickColor GetColor(int i, int maxI)
    {
        if (maxI >= 16777216)
        {
            throw new FormatException("MaxI is too big. MaxI must be less than 16777216, to ensure that the color is unique for each i");
        }
        if (i < 0 || i >= maxI)
        {
            throw new FormatException("i must be between 0 and maxI");
        }


        // max 24bit color
        const int maxColor = 256 * 256 * 256;
        var color = maxColor / maxI * i;

        byte r = (byte)((color & 0x0000FF) >> 0);
        byte g = (byte)((color & 0x00FF00) >> 8);
        byte b = (byte)((color & 0xFF0000) >> 16);

        var c = new MagickColor(r, g, b);

        return c;
    }

}