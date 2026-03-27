using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;

namespace Converter;

public static class BiomeConverter
{
    private static void WriteMaskFromCells(
        Map map,
        Drawing.Canvas maskCanvas,
        Drawing.Canvas detailIndex,
        Drawing.Canvas detailIntensity,
        IReadOnlyList<Cell> cells,
        AzgaarBiome biomeId)
    {
        var ck3Biome = biomeId.ToCk3Biome();
        MyConsole.Info($"started writing {ck3Biome.ToMaskFilename()}", true);

        var ptsList = cells.Select(cell =>
            cell.cells
                .Select(n => Helper.GeoToPixel(n[0], n[1], map))
                .Select(p => ((float)p.X, (float)p.Y))
                .ToArray()
        ).ToArray();

        maskCanvas.MakeCurrent();
        maskCanvas.Clear(Drawing.RgbaColor.Black);
        foreach (var pts in ptsList)
            maskCanvas.DrawFilledPolygon(pts, Drawing.RgbaColor.White);

        var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks", ck3Biome.ToMaskFilename());
        Helper.EnsureDirectoryExists(path);
        maskCanvas.SaveAsPng(path);

        var indexColor = Drawing.RgbaColor.FromBytes((byte)(int)ck3Biome, 255, 255, 255);
        detailIndex.MakeCurrent();
        foreach (var pts in ptsList)
            detailIndex.DrawFilledPolygon(pts, indexColor);

        detailIntensity.MakeCurrent();
        foreach (var pts in ptsList)
            detailIntensity.DrawFilledPolygon(pts, Drawing.RgbaColor.FromBytes(255, 0, 0));

        MyConsole.Info($"Finished writing {ck3Biome.ToMaskFilename()}", true);
    }

    public static async Task WriteMasks(Map map)
    {
        // Resize default maps
        {
            var templateFile = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks", "winter_effect_mask.png");
            var masks = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks");
            var masks_gen = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks_gen");
            var files = Directory.EnumerateFiles(masks).Concat(Directory.EnumerateFiles(masks_gen)).Where(n => n.EndsWith(".png")).Except([templateFile]);
            using (var img1 = new MagickImage(templateFile))
            {
                img1.Resize(map.Settings.MapWidth, map.Settings.MapHeight);
                img1.Write(templateFile, MagickFormat.Png00);
            }
            foreach (var path in files)
            {
                File.Copy(templateFile, path, true);
            }
        }

        var cellsByBiome = map.Output.Provinces
            .Skip(1)
            .Where(n => !n.IsWater)
            .SelectMany(n => n.Cells)
            .GroupBy(n => n.biome)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Cell>)g.ToList());

        using var maskCanvas = new Drawing.Canvas(map.Settings.MapWidth, map.Settings.MapHeight);
        using var detailIndexCanvas = new Drawing.Canvas(map.Settings.MapWidth, map.Settings.MapHeight);
        using var detailIntensityCanvas = new Drawing.Canvas(map.Settings.MapWidth, map.Settings.MapHeight);

        detailIndexCanvas.MakeCurrent();
        detailIndexCanvas.Clear(Drawing.RgbaColor.White);

        detailIntensityCanvas.MakeCurrent();
        detailIntensityCanvas.Clear(Drawing.RgbaColor.Black);

        AzgaarBiome[] biomes = [
            AzgaarBiome.HotDesert,
            AzgaarBiome.ColdDesert,
            AzgaarBiome.Savanna,
            AzgaarBiome.Grassland,
            AzgaarBiome.TropicalSeasonalForest,
            AzgaarBiome.TemperateDeciduousForest,
            AzgaarBiome.TropicalRainforest,
            AzgaarBiome.TemperateRainforest,
            AzgaarBiome.Taiga,
            AzgaarBiome.Tundra,
            AzgaarBiome.Glacier,
            AzgaarBiome.Wetland,
        ];

        foreach (var biomeId in biomes)
        {
            if (!cellsByBiome.TryGetValue((int)biomeId, out var cells))
            {
                MyConsole.Info($"No cells for biome {biomeId}, skipping.", true);
                continue;
            }
            WriteMaskFromCells(map, maskCanvas, detailIndexCanvas, detailIntensityCanvas, cells, biomeId);
        }

        // write detail_index
        {
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "detail_index.tga");
            Helper.EnsureDirectoryExists(path);
            detailIndexCanvas.MakeCurrent();
            var pixels = detailIndexCanvas.GetPixelsTopLeft();
            using var img = Image.LoadPixelData<Rgba32>(pixels, map.Settings.MapWidth, map.Settings.MapHeight);
            var encoder = new TgaEncoder { BitsPerPixel = TgaBitsPerPixel.Pixel32, Compression = TgaCompression.None };
            await img.SaveAsync(path, encoder);
        }

        // write detail_intensity
        {
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "detail_intensity.tga");
            Helper.EnsureDirectoryExists(path);
            detailIntensityCanvas.MakeCurrent();
            var pixels = detailIntensityCanvas.GetPixelsTopLeft();
            using var img = Image.LoadPixelData<Rgba32>(pixels, map.Settings.MapWidth, map.Settings.MapHeight);
            var encoder = new TgaEncoder { BitsPerPixel = TgaBitsPerPixel.Pixel32, Compression = TgaCompression.None };
            await img.SaveAsync(path, encoder);
        }

        // resize colormap
        {
            var inputPath = Helper.GetPath(Settings.Instance.TotalConversionSandboxPath, "gfx", "map", "terrain", "colormap.dds");
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "colormap.dds");
            Helper.EnsureDirectoryExists(path);
            using var img = new MagickImage(inputPath);
            img.Resize(map.Settings.MapWidth / 4, map.Settings.MapHeight / 4);
            img.Write(path);
        }
    }
}

