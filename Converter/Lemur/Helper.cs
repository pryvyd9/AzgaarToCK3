using ImageMagick;

namespace Converter.Lemur;

public static class Helper
{
    public static PointD GeoToPixel(float lon, float lat, Entities.Map map)
    {
        return new PointD((lon - map.XOffset) * map.XRatio, Map.MapHeight - (lat - map.YOffset) * map.YRatio);
    }

}