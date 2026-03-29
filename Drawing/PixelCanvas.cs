using SkiaSharp;

namespace Drawing;

public sealed class PixelCanvas : IDisposable
{
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _canvas;

    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;

    public PixelCanvas(int width, int height, SKColor background)
    {
        _bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        _canvas = new SKCanvas(_bitmap);
        _canvas.Clear(background);
    }

    public void FillSvgPath(string svgPathData, SKColor color, float scaleX, float scaleY)
    {
        using var rawPath = SKPath.ParseSvgPathData(svgPathData);
        if (rawPath is null) return;
        using var scaledPath = new SKPath();
        rawPath.Transform(SKMatrix.CreateScale(scaleX, scaleY), scaledPath);
        using var paint = MakeFillPaint(color);
        _canvas.DrawPath(scaledPath, paint);
    }

    public void FillPolygon(IEnumerable<(float x, float y)> points, SKColor color)
    {
        using var path = new SKPath();
        using var paint = MakeFillPaint(color);
        bool first = true;
        foreach (var (x, y) in points)
        {
            if (first) { path.MoveTo(x, y); first = false; }
            else path.LineTo(x, y);
        }
        path.Close();
        _canvas.DrawPath(path, paint);
    }

    public void DrawPolyline(IEnumerable<(float x, float y)> points, SKColor color, float strokeWidth)
    {
        using var path = new SKPath();
        using var paint = MakeStrokePaint(color, strokeWidth);
        bool first = true;
        foreach (var (x, y) in points)
        {
            if (first) { path.MoveTo(x, y); first = false; }
            else path.LineTo(x, y);
        }
        _canvas.DrawPath(path, paint);
    }

    /// <summary>L8 byte array for ImageSharp Image.LoadPixelData&lt;L8&gt;</summary>
    public byte[] GetGrayscaleBytes()
    {
        int n = _bitmap.Width * _bitmap.Height;
        var result = new byte[n];
        var pixels = _bitmap.Pixels;
        for (int i = 0; i < n; i++)
        {
            var c = pixels[i];
            result[i] = (byte)(c.Red * 0.299f + c.Green * 0.587f + c.Blue * 0.114f);
        }
        return result;
    }

    /// <summary>RGBA32 byte array for ImageSharp Image.LoadPixelData&lt;Rgba32&gt;</summary>
    public byte[] GetRgbaBytes() => _bitmap.GetPixelSpan().ToArray();

    /// <summary>Saves as PNG directly via SkiaSharp</summary>
    public void SaveAsPng(string path)
    {
        using var image = SKImage.FromBitmap(_bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    private static SKPaint MakeFillPaint(SKColor color) => new()
    {
        Color = color,
        IsAntialias = false,
        Style = SKPaintStyle.Fill,
        BlendMode = SKBlendMode.Src,
    };

    private static SKPaint MakeStrokePaint(SKColor color, float width) => new()
    {
        Color = color,
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = width,
        BlendMode = SKBlendMode.Src,
    };

    public void Dispose()
    {
        _canvas.Dispose();
        _bitmap.Dispose();
    }
}
