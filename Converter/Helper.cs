using ImageMagick;
using System.Drawing.Imaging;
using System.Drawing;
using System.Text;
using Svg;
using SixLabors.ImageSharp;

namespace Converter;

public enum AzgaarBiome
{
    HotDesert = 1,
    ColdDesert,
    Savanna,
    Grassland,
    TropicalSeasonalForest,
    TemperateDeciduousForest,
    TropicalRainforest,
    TemperateRainforest,
    Taiga,
    Tundra,
    Glacier,
    Wetland,
}

public enum CK3Biome
{
    Plains_01 = 0,
    Plains_01_dry,
    Plains_01_dry_mud,
    Plains_01_rough,
    Plains_01_noisy,
    Farmland_01,
    Mud_wet_01,
    Beach_02,
    Beach_02_pebbles,
    Beach_02_mediterranean,
    Hills_01,
    Hills_01_rocks,
    Hills_01_rocks_Medi,
    Hills_01_rocks_small,
    Floodplains_01,
    Wetlands_02 = 15,
    Wetlands_02_mud,
    Coastline_cliff_grey,
    Mountain_02,
    Mountain_02_b,
    Mountain_02_d,
    Mountain_02_d_valleys,
    Mountain_02_d_snow,
    Mountain_02_d_desert,
    Mountain_02_snow,
    Mountain_02_c,
    Mountain_02_c_snow,
    Forest_Leaf_01 = 28,
    //Forestfloor_02,
    Forest_jungle_01,
    Forest_pine_01 = 30,
    Forestfloor,
    Desert_01,
    Desert_02,
    Desert_cracked,
    Desert_wavy_01,
    Desert_wavy_01_larger,
    Desert_flat_01,
    Desert_rocky,
    Mountain_02_desert,
    Mountain_02_desert_c,
    Drylands_01,
    Drylands_01_grassy,
    Drylands_01_cracked,
    Oasis,
    Medi_dry_mud,
    Medi_grass_01,
    Medi_lumpy_grass,
    Medi_noisy_grass,
    Medi_farmlands,
    Northern_plains_01 = 50,
    Steppe_grass,
    Steppe_rocks,
    Steppe_bushes,
    Snow = 54,
    India_farmlands,
}

public static class Helper
{
    // Generates path that works in both Windows and Mac
    public static string GetPath(params string[] paths)
    {
        if (paths is null) return "";
        try
        {
            return Path.Combine(paths.SelectMany(n => n.Replace(@"\\", "/").Replace(@"\", "/").Split("/")).ToArray());
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to combine paths: {string.Join(",", paths)}", ex);
        }
    }

    public static void EnsureDirectoryExists(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public static bool IsCellHighMountains(int height)
    {
        return height > 2000;
    }
    public static bool IsCellMountains(int height)
    {
        return height is > 1000 and <= 2000;
    }
    public static bool IsCellLowMountains(int height)
    {
        return height is > 500 and <= 1000;
    }
    public static bool IsCellHills(int biomeId, int height)
    {
        if (IsCellHighMountains(height) || IsCellMountains(height) || IsCellLowMountains(height))
        {
            return false;
        }
        return IsHills(biomeId, height);
    }
    private static bool IsHills(int biomeId, int heightDifference)
    {
        return heightDifference > 200 && biomeId is 4 or 5 or 6 or 7 or 8;
    }
    public static string MapBiome(int biomeId)
    {
        return MapBiome((AzgaarBiome)biomeId);
    }
    public static string MapBiome(AzgaarBiome biomeId)
    {
        return biomeId switch
        {
            AzgaarBiome.HotDesert => "desert",// Hot desert > desert
            AzgaarBiome.ColdDesert => "taiga",// Cold desert > taiga
            AzgaarBiome.Savanna => "steppe",// Savanna > steppe
            AzgaarBiome.Grassland => "plains",// Grassland > plains
            AzgaarBiome.TropicalSeasonalForest => "farmlands",// Tropical seasonal forest > farmlands
            AzgaarBiome.TemperateDeciduousForest => "forest",// Temperate deciduous forest > forest
            AzgaarBiome.TropicalRainforest => "jungle",// Tropical rainforest > jungle
            AzgaarBiome.TemperateRainforest => "forest",// "Temperate rainforest" > forest
            AzgaarBiome.Taiga => "taiga",// Taiga > taiga
            AzgaarBiome.Tundra => "taiga",// Tundra > taiga
            AzgaarBiome.Glacier => "drylands",// Glacier > floodplains
            AzgaarBiome.Wetland => "floodplains",// Wetland > wetlands
            _ => throw new ArgumentException("Unrecognized biomeId")
        };
    }
    public static CK3Biome ToCk3Biome(this AzgaarBiome biomeId)
    {
        return biomeId switch
        {
            AzgaarBiome.HotDesert => CK3Biome.Desert_01,
            AzgaarBiome.ColdDesert => CK3Biome.Desert_02,
            AzgaarBiome.Savanna => CK3Biome.Plains_01_dry,
            AzgaarBiome.Grassland => CK3Biome.Plains_01,
            AzgaarBiome.TropicalSeasonalForest => CK3Biome.Farmland_01,
            AzgaarBiome.TemperateDeciduousForest => CK3Biome.Forest_Leaf_01,
            AzgaarBiome.TropicalRainforest => CK3Biome.Forest_jungle_01,
            AzgaarBiome.TemperateRainforest => CK3Biome.Forest_pine_01,
            AzgaarBiome.Taiga => CK3Biome.Forestfloor,
            AzgaarBiome.Tundra => CK3Biome.Northern_plains_01,
            AzgaarBiome.Glacier => CK3Biome.Snow,
            AzgaarBiome.Wetland => CK3Biome.Floodplains_01,
            _ => throw new ArgumentException("Unrecognized biomeId")
        };
    }
    public static string? GetProvinceBiomeName(int biomeId, int heightDifference)
    {
        // Marine > ocean
        if (biomeId == 0)
            return null;

        // plains/farmlands/hills/mountains/desert/desert_mountains/oasis/jungle/forest/taiga/wetlands/steppe/floodplains/drylands
        /*
         * 	0"Marine",
			1"Hot desert",
			2"Cold desert",
			3"Savanna",
			4"Grassland",
			5"Tropical seasonal forest",
			6"Temperate deciduous forest",
			7"Tropical rainforest",
			8"Temperate rainforest",
			9"Taiga",
			10"Tundra",
			11"Glacier",
			12"Wetland"
         * */
        if (IsCellHighMountains(heightDifference))
        {
            return biomeId switch
            {
                1 or 3 => "desert_mountains",
                _ => "mountains",
            };
        }
        else if (IsCellMountains(heightDifference))
        {
            return biomeId switch
            {
                1 => "drylands",
                2 => "taiga",
                3 => "drylands",
                4 => "hills",
                5 or 6 or 7 or 8 => "taiga",
                9 or 10 or 11 => "mountains",
                12 => "farmlands",
                _ => throw new ArgumentException("Unrecognized biomeId")
            };
        }
        else if (IsHills(biomeId, heightDifference))
        {
            return biomeId switch
            {
                1 => "oasis",
                2 => "steppe",
                3 => "hills",
                4 => "hills",
                5 or 6 or 7 or 8 => "taiga",
                9 or 10 or 11 => "drylands",
                12 => "wetlands",
                _ => throw new ArgumentException("Unrecognized biomeId")
            };
        }
        return MapBiome(biomeId);
    }

    public static string ToMaskFilename(this CK3Biome biome)
    {
        return biome.ToString().ToLower() + "_mask.png";
    }

    public static double Percentile(int[] sequence, double excelPercentile)
    {
        Array.Sort(sequence);
        int N = sequence.Length;
        double n = (N - 1) * excelPercentile + 1;
        // Another method: double n = (N + 1) * excelPercentile;
        if (n == 1d) return sequence[0];
        else if (n == N) return sequence[N - 1];
        else
        {
            int k = (int)n;
            double d = n - k;
            return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
        }
    }
    public static double Percentile(float[] sequence, double excelPercentile)
    {
        Array.Sort(sequence);
        int N = sequence.Length;
        double n = (N - 1) * excelPercentile + 1;
        // Another method: double n = (N + 1) * excelPercentile;
        if (n == 1d) return sequence[0];
        else if (n == N) return sequence[N - 1];
        else
        {
            int k = (int)n;
            double d = n - k;
            return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
        }
    }

    public static double HeightDifference(Province province)
    {
        var heights = province.Cells.Select(n => n.height).ToArray();
        return Percentile(heights, 0.7) - Percentile(heights, 0.3);
    }

    public static PointD GeoToPixel(float lon, float lat, Map map)
    {
        return new PointD((lon - map.XOffset) * map.XRatio, map.Settings.MapHeight - (lat - map.YOffset) * map.YRatio);
    }
    public static PointD GeoToPixelCrutch(float lon, float lat, Map map)
    {
        return new PointD((lon - map.XOffset) * map.XRatio, (lat - map.YOffset) * map.YRatio);
    }
    public static PointD PixelToFullPixel(float x, float y, Map map)
    {
        return new PointD(x * map.PixelXRatio, map.Settings.MapHeight - y * map.PixelYRatio);
    }

    /// <summary>
    /// Examples:
    /// 1. await WriteLocalizationFile(map, "dynasties", "dynasty_names_l_", content)
    ///    localization\{language}\dynasties\{filePrefix}{language}.yml
    /// 2. await WriteLocalizationFile(map, null, "dynasty_names_l_", content)
    ///    localization\{language}\{filePrefix}{language}.yml
    /// </summary>
    /// <param name="localizationPath">pass null if in localization root</param>
    /// <param name="filePrefix">dynasty_names_l_</param>
    public static async Task WriteLocalizationFile(Map map, string? localizationPath, string filePrefix, string content, string lastHeaderLineContains)
    {
        var languages = new string[] { "english", "french", "german", "korean", "russian", "simp_chinese", "spanish" };

        foreach (var language in languages)
        {
            var fileName = $"{filePrefix}{language}.yml";

            var originalFilePath = localizationPath is null
                ? GetPath(map.Settings.Ck3Directory, "localization", language, fileName)
                : GetPath(map.Settings.Ck3Directory, "localization", language, localizationPath, fileName);

            bool isLastHeaderLineReached = false;
            var header = File.ReadLines(originalFilePath).TakeWhile(n =>
            {
                if (isLastHeaderLineReached)
                    return false;

                isLastHeaderLineReached = n.Contains(lastHeaderLineContains);
                return true;
            }).ToArray();

            var file = $"{string.Join("\n", header)}\n\n {content}";

            var outputPath = localizationPath is null
                ? GetPath(Settings.OutputDirectory, "localization", language, fileName)
                : GetPath(Settings.OutputDirectory, "localization", language, localizationPath, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            await File.WriteAllTextAsync(outputPath, file, new UTF8Encoding(true));
        }
    }


    public static unsafe Bitmap ToGrayscale(Bitmap colorBitmap)
    {
        int Width = colorBitmap.Width;
        int Height = colorBitmap.Height;

        Bitmap grayscaleBitmap = new Bitmap(Width, Height, PixelFormat.Format8bppIndexed);

        grayscaleBitmap.SetResolution(colorBitmap.HorizontalResolution,
                             colorBitmap.VerticalResolution);

        ///////////////////////////////////////
        // Set grayscale palette
        ///////////////////////////////////////
        ColorPalette colorPalette = grayscaleBitmap.Palette;
        for (int i = 0; i < colorPalette.Entries.Length; i++)
        {
            colorPalette.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
        }
        grayscaleBitmap.Palette = colorPalette;
        ///////////////////////////////////////
        // Set grayscale palette
        ///////////////////////////////////////
        BitmapData bitmapData = grayscaleBitmap.LockBits(
            new System.Drawing.Rectangle(System.Drawing.Point.Empty, grayscaleBitmap.Size),
            ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

        Byte* pPixel = (Byte*)bitmapData.Scan0;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                System.Drawing.Color clr = colorBitmap.GetPixel(x, y);

                Byte byPixel = (byte)((30 * clr.R + 59 * clr.G + 11 * clr.B) / 100);

                pPixel[x] = byPixel;
            }

            pPixel += bitmapData.Stride;
        }

        grayscaleBitmap.UnlockBits(bitmapData);

        return grayscaleBitmap;
    }

    public static unsafe Bitmap ToBitmap(byte[] colorBitmap, int width, int height)
    {
        Bitmap grayscaleBitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

        ///////////////////////////////////////
        // Set grayscale palette
        ///////////////////////////////////////
        ColorPalette colorPalette = grayscaleBitmap.Palette;
        for (int i = 0; i < colorPalette.Entries.Length; i++)
        {
            colorPalette.Entries[i] = System.Drawing.Color.FromArgb(i, i, i);
        }
        grayscaleBitmap.Palette = colorPalette;
        ///////////////////////////////////////
        // Set grayscale palette
        ///////////////////////////////////////
        BitmapData bitmapData = grayscaleBitmap.LockBits(
            new System.Drawing.Rectangle(System.Drawing.Point.Empty, grayscaleBitmap.Size),
            ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

        Byte* pPixel = (Byte*)bitmapData.Scan0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pPixel[x] = colorBitmap[width * y + x];
            }

            pPixel += bitmapData.Stride;
        }

        grayscaleBitmap.UnlockBits(bitmapData);

        return grayscaleBitmap;
    }


    public static unsafe byte[] ToGrayscaleByteArray(Bitmap colorBitmap)
    {
        int Width = colorBitmap.Width;
        int Height = colorBitmap.Height;

        byte[] array = new byte[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                System.Drawing.Color clr = colorBitmap.GetPixel(x, y);

                Byte byPixel = (byte)((30 * clr.R + 59 * clr.G + 11 * clr.B) / 100);

                array[Width * y + x] = byPixel;
            }
        }

        return array;
    }


    public static unsafe byte[] ToRGBAByteArray(Bitmap colorBitmap)
    {
        int Width = colorBitmap.Width;
        int Height = colorBitmap.Height;
        int stride = 4;

        byte[] array = new byte[Width * Height * stride];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                System.Drawing.Color clr = colorBitmap.GetPixel(x, y);

                array[Width * stride * y + x * stride + 0] = clr.R;
                array[Width * stride * y + x * stride + 1] = clr.G;
                array[Width * stride * y + x * stride + 2] = clr.B;
                array[Width * stride * y + x * stride + 3] = clr.A;
            }
        }

        return array;
    }

    public static SixLabors.ImageSharp.Image ToGrayscaleImage(this SvgDocument svgDocument, int width, int height)
    {
        var bitmap = svgDocument.Draw(width, height);
        var img = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.L8>(Helper.ToGrayscaleByteArray(bitmap).AsSpan(), width, height);
        //img.Mutate(x => x.Grayscale(GrayscaleMode.Bt709));
        return img;
    }

    public static SixLabors.ImageSharp.Image ToImage(this SvgDocument svgDocument, int width, int height)
    {
        var bitmap = svgDocument.Draw(width, height);
        var img = SixLabors.ImageSharp.Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Rgba32>(Helper.ToRGBAByteArray(bitmap).AsSpan(), width, height);
        return img;
    }

}
