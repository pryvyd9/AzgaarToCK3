using Drawing;
using ImageMagick;
using SkiaSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Numerics;
using System.Xml;

namespace Converter;

public static class BiomeConverter
{
    private static readonly SemaphoreSlim _detail_index_semaphore = new(1);
    private static readonly SemaphoreSlim _detail_intensity_semaphore = new(1);

    private static async Task WriteMaskFromXml(
        Map map,
        PixelCanvas detail_index_canvas,
        PixelCanvas detail_intensity_canvas,
        float scaleX,
        float scaleY,
        AzgaarBiome biomeId)
    {
        if (Settings.Instance.MaxThreads > 1)
        {
            await Task.Yield();
        }

        var ck3Biome = biomeId.ToCk3Biome();
        MyConsole.Info($"started writing {ck3Biome.ToMaskFilename()}", true);

        // create ns manager
        XmlNamespaceManager xmlnsManager = new(map.Input.XmlMap.NameTable);
        xmlnsManager.AddNamespace("ns", "http://www.w3.org/2000/svg");

        XmlElement? GetNodeFromDoc(string attribute) => map.Input.XmlMap.SelectSingleNode($"//*[{attribute}]", xmlnsManager) as XmlElement;

        var biomeAttribute = $"@id='biome{(int)biomeId}'";
        var biomeElement = GetNodeFromDoc(biomeAttribute);
        if (biomeElement is null)
        {
            return;
        }

        // Collect all SVG path data: the biome element itself may be a <path> (common in Azgaar FMG)
        // or a <g> group containing child paths. Handle both cases and any nesting depth.
        var pathData = new List<string>();
        {
            var selfD = biomeElement.GetAttribute("d");
            if (!string.IsNullOrEmpty(selfD)) pathData.Add(selfD);
            foreach (XmlElement el in biomeElement.SelectNodes(".//*", xmlnsManager)!.OfType<XmlElement>())
            {
                var d = el.GetAttribute("d");
                if (!string.IsNullOrEmpty(d)) pathData.Add(d);
            }
        }

        if (pathData.Count == 0)
        {
            MyConsole.Info($"No path data found for {ck3Biome.ToMaskFilename()}, skipping", true);
            return;
        }

        var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks", ck3Biome.ToMaskFilename());
        Helper.EnsureDirectoryExists(path);

        // Render grayscale biome mask: white polygon on black background
        using (var canvas = new PixelCanvas(map.Settings.MapWidth, map.Settings.MapHeight, SKColors.Black))
        {
            foreach (var d in pathData)
                canvas.FillSvgPath(d, SKColors.White, scaleX, scaleY);
            canvas.SaveAsPng(path);
        }

        // Detail Index
        {
            var indexColor = new SKColor((byte)ck3Biome, 255, 255, 255);

            await _detail_index_semaphore.WaitAsync();
            try
            {
                foreach (var d in pathData)
                    detail_index_canvas.FillSvgPath(d, indexColor, scaleX, scaleY);
            }
            finally
            {
                _detail_index_semaphore.Release();
            }
        }

        // Detail Intensity
        {
            var intensityColor = new SKColor(255, 0, 0, 255);

            await _detail_intensity_semaphore.WaitAsync();
            try
            {
                foreach (var d in pathData)
                    detail_intensity_canvas.FillSvgPath(d, intensityColor, scaleX, scaleY);
            }
            finally
            {
                _detail_intensity_semaphore.Release();
            }
        }

        MyConsole.Info($"Finished writing {ck3Biome.ToMaskFilename()}", true);
    }

    public static async Task WriteMasks(Map map)
    {
        // create ns manager
        XmlNamespaceManager xmlnsManager = new(map.Input.XmlMap.NameTable);
        xmlnsManager.AddNamespace("ns", "http://www.w3.org/2000/svg");

        XmlElement? GetNodeFromDoc(string attribute) => map.Input.XmlMap.SelectSingleNode($"//*[{attribute}]", xmlnsManager) as XmlElement;
        var biomesElement = GetNodeFromDoc("@id='svgbiomes'")!;

        float svgWidth = float.Parse(biomesElement.GetAttribute("width"), System.Globalization.CultureInfo.InvariantCulture);
        float svgHeight = float.Parse(biomesElement.GetAttribute("height"), System.Globalization.CultureInfo.InvariantCulture);
        float scaleX = map.Settings.MapWidth / svgWidth;
        float scaleY = map.Settings.MapHeight / svgHeight;

        int w = map.Settings.MapWidth;
        int h = map.Settings.MapHeight;

        // Resize default maps
        {
            var templateFile = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks", "winter_effect_mask.png");
            var masks = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks");
            var masks_gen = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks_gen");
            var files = Directory.EnumerateFiles(masks).Concat(Directory.EnumerateFiles(masks_gen)).Where(n => n.EndsWith(".png")).Except([templateFile]);
            using (var img1 = new MagickImage(templateFile))
            {
                img1.Resize(w, h);
                img1.Write(templateFile, MagickFormat.Png00);
            }

            foreach (var path in files)
            {
                File.Copy(templateFile, path, true);
            }
        }

        using var detail_index_canvas = new PixelCanvas(w, h, SKColors.White);
        using var detail_intensity_canvas = new PixelCanvas(w, h, SKColors.Black);

        Task[] tasks = [
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.HotDesert),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.ColdDesert),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.Savanna),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.Grassland),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.TropicalSeasonalForest),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.TemperateDeciduousForest),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.TropicalRainforest),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.TemperateRainforest),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.Taiga),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.Tundra),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.Glacier),
            WriteMaskFromXml(map, detail_index_canvas, detail_intensity_canvas, scaleX, scaleY, AzgaarBiome.Wetland),
        ];

        await Task.WhenAll(tasks);

        // write detail_index
        {
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "detail_index.tga");
            Helper.EnsureDirectoryExists(path);

            var img = Image.LoadPixelData<Rgba32>(detail_index_canvas.GetRgbaBytes().AsSpan(), w, h);

            // Set water mask to mud_wet_01 biome how it's done in the game.
            img.Mutate(n => n.ProcessPixelRowsAsVector4((row) =>
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (row[i][0] == 1)
                    {
                        row[i] = (Vector4)Color.FromRgba((byte)CK3Biome.mud_wet_01, 255, 255, 255);
                    }
                }
            }));

            var encoder = new TgaEncoder
            {
                BitsPerPixel = TgaBitsPerPixel.Pixel32,
                Compression = TgaCompression.None,
            };
            await img.SaveAsync(path, encoder);
        }

        // write detail_intensity
        {
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "detail_intensity.tga");
            Helper.EnsureDirectoryExists(path);

            var img = Image.LoadPixelData<Rgba32>(detail_intensity_canvas.GetRgbaBytes().AsSpan(), w, h);

            img.Mutate(n => n.ProcessPixelRowsAsVector4((row) =>
            {
                // Workaround to bypass reader's optimization reads alpha as 255 when the whole alpha channel is 0.
                row[0][3] = 0.01f;
                for (int i = 1; i < row.Length; i++)
                {
                    row[i][3] = 0;
                }

                // Set intensity for water mask to 255 so it's displayed properly.
                // Otherwise, it's going to be displayed as black void.
                // With current approach when we only write to red channel for simplicity the whole red channel is going to be filled with 255.
                for (int i = 0; i < row.Length; i++)
                {
                    if (row[i][0] == 0)
                    {
                        row[i][0] = 1;
                    }
                }
            }));

            var encoder = new TgaEncoder
            {
                BitsPerPixel = TgaBitsPerPixel.Pixel32,
                Compression = TgaCompression.None,
            };
            await img.SaveAsync(path, encoder);
        }

        // resize colormap
        {
            var inputPath = Helper.GetPath(Settings.Instance.TotalConversionSandboxPath, "gfx", "map", "terrain", "colormap.dds");
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "colormap.dds");
            Helper.EnsureDirectoryExists(path);

            using var img = new MagickImage(inputPath);
            img.Resize(w / 4, h / 4);
            img.Write(path);
        }
    }
}
