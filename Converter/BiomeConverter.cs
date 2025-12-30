using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tga;
using Svg;
using System.Xml;

namespace Converter;

public static class BiomeConverter
{
    private static readonly SemaphoreSlim _detail_index_semaphore = new(1);
    private static readonly SemaphoreSlim _detail_intensity_semaphore = new(1);

    private static async Task WriteMaskFromXml(Map map, XmlDocument detail_index, XmlDocument detail_intensity, AzgaarBiome biomeId)
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
        XmlElement? GetNode(XmlNode xmlElement, string attribute) => xmlElement.SelectSingleNode($"//*[{attribute}]", xmlnsManager) as XmlElement;
        void RemoveExcept(XmlNode xmlElement, string attribute)
        {
            XmlElement? childExcept = GetNode(xmlElement, attribute);
            List<XmlElement> childrenToRemove = [];
            foreach (XmlElement c in xmlElement.ChildNodes)
            {
                if (c != childExcept)
                {
                    childrenToRemove.Add(c);
                }
            }

            foreach (var c in childrenToRemove)
            {
                c!.ParentNode!.RemoveChild(c);
            }
        }

        var doc = new XmlDocument();
        doc.LoadXml(GetNodeFromDoc("@id='svgbiomes'")!.OuterXml);
        var biomeAttribute = $"@id='biome{(int)biomeId}'";
        var biome = GetNode(doc, biomeAttribute);
        if (biome is null)
        {
            return;
        }
        biome.SetAttribute("stroke", "white");
        biome.SetAttribute("fill", "white");

        var biomes = GetNode(doc, "@id='biomes'");
        //biomes!.SetAttribute("filter", "url(#blur10)");

        RemoveExcept(biomes, biomeAttribute);

        var defs = doc.CreateDocumentFragment();
        defs.InnerXml = @"<defs><filter id=""blur10""><feGaussianBlur in=""SourceGraphic"" stdDeviation=""10"" /></filter></defs>";
        doc.DocumentElement.FirstChild.PrependChild(defs);

        var svg = SvgDocument.FromSvg<SvgDocument>(doc.OuterXml);
        var img = svg.ToGrayscaleImage(map.Settings.MapWidth, map.Settings.MapHeight);

        var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks", ck3Biome.ToMaskFilename());
        Helper.EnsureDirectoryExists(path);
        //img.Save($"{filename}.png");
        await img.SaveAsync(path);

        // Detail Index
        {
            var indexColor = $"rgba({(int)ck3Biome},255,255,255)";
            biome.SetAttribute("stroke", indexColor);
            biome.SetAttribute("fill", indexColor);

            var detail_index_biome = detail_index.CreateDocumentFragment();
            detail_index_biome.InnerXml = biome.OuterXml;
            detail_index.DocumentElement!.AppendChild(detail_index_biome);

            var detail_index_svg = GetNode(detail_index, "@id='detail_index'")!;

            await _detail_index_semaphore.WaitAsync();
            try
            {
                detail_index_svg.AppendChild(detail_index_biome);
            }
            finally
            {
                _detail_index_semaphore.Release();
            }
        }
        // Detail Intensity
        {
            var intensityColor = $"rgba(255,0,0,1)";
            biome.SetAttribute("stroke", intensityColor);
            biome.SetAttribute("fill", intensityColor);

            var detail_intensity_biome = detail_intensity.CreateDocumentFragment();
            detail_intensity_biome.InnerXml = biome.OuterXml;
            detail_intensity.DocumentElement!.AppendChild(detail_intensity_biome);

            var detail_intensity_svg = GetNode(detail_intensity, "@id='detail_intensity'")!;

            await _detail_intensity_semaphore.WaitAsync();
            try
            {
                detail_intensity_svg.AppendChild(detail_intensity_biome);
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
        var biomes = GetNodeFromDoc("@id='svgbiomes'")!;
        var width = biomes.GetAttribute("width");
        var height = biomes.GetAttribute("height");

        var detail_index = new XmlDocument();
        {
            detail_index.LoadXml($"""
                <svg id="detail_index" width="{width}" height="{height}" version="1.1" background-color="white" >
                <rect x="0" y="0" width="100%" height="100%" fill="white" />
                </svg>
                """);
        }


        var detail_intensity = new XmlDocument();
        {
            detail_intensity.LoadXml($"""
                <svg id="detail_intensity" width="{width}" height="{height}" version="1.1" background-color="red" >
                <rect x="0" y="0" width="100%" height="100%" fill="black" />
                </svg>
                """);
        }
        // Resize default maps
        {
            var templateFile = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks", "winter_effect_mask.png");
            var masks = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks");
            var masks_gen = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "masks_gen");
            var files = Directory.EnumerateFiles(masks).Concat(Directory.EnumerateFiles(masks_gen)).Where(n => n.EndsWith(".png")).Except([templateFile]);
            using (var img1 = new MagickImage(templateFile))
            {
                img1.Resize(map.Settings.MapWidth, map.Settings.MapHeight);
                img1.Write(templateFile);
            }

            foreach (var path in files)
            {
                File.Copy(templateFile, path, true);
            }
        }

        Task[] tasks = [
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.HotDesert),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.ColdDesert),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.Savanna),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.Grassland),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.TropicalSeasonalForest),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.TemperateDeciduousForest),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.TropicalRainforest),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.TemperateRainforest),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.Taiga),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.Tundra),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.Glacier),
            WriteMaskFromXml(map, detail_index, detail_intensity, AzgaarBiome.Wetland),
        ];

        await Task.WhenAll(tasks);

        // write detail_index
        {
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "detail_index.tga");
            Helper.EnsureDirectoryExists(path);
            var svg = SvgDocument.FromSvg<SvgDocument>(detail_index.OuterXml);
            var img = svg.ToImage(map.Settings.MapWidth, map.Settings.MapHeight);
            var encoder = new TgaEncoder
            {
                BitsPerPixel = TgaBitsPerPixel.Pixel32,
                Compression = TgaCompression.None,
            };
            //await img.SaveAsync("detail_index.tga", encoder);
            await img.SaveAsync(path, encoder);
        }

        // write detail_intensity
        {
            var path = Helper.GetPath(Settings.OutputDirectory, "gfx", "map", "terrain", "detail_intensity.tga");
            Helper.EnsureDirectoryExists(path);
            var svg = SvgDocument.FromSvg<SvgDocument>(detail_intensity.OuterXml);
            var img = svg.ToImage(map.Settings.MapWidth, map.Settings.MapHeight);
            var encoder = new TgaEncoder
            {
                BitsPerPixel = TgaBitsPerPixel.Pixel32,
                Compression = TgaCompression.None,
            };
            //await img.SaveAsync("detail_intensity.tga", encoder);
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

        //var nonWaterProvinceCells = map.Output.Provinces
        //    .Skip(1)
        //    .Where(n => !n.IsWater && n.Cells.Any())
        //    .SelectMany(n => n.Cells)
        //    .ToArray();

        //var provinceBiomes = map.Output.Provinces
        //    .Skip(1)
        //    .Where(n => !n.IsWater && n.Cells.Any())
        //    .Select(n =>
        //    {
        //        var primaryBiome = n.Cells.Select(m => m.biome).Max();
        //        var heightDifference = (int)Helper.HeightDifference(n);
        //        return new
        //        {
        //            Province = n,
        //            Biome = Helper.GetProvinceBiomeName(primaryBiome, heightDifference)
        //        };
        //    }).ToArray();



        //// taiga
        //await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is var b && (b == "taiga" || b == "drylands" && Helper.IsCellLowMountains(n.height) || b == "drylands" && Helper.IsCellMountains(n.height))), map, "forest_pine_01_mask");
        //// Desert
        //await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "desert" && !Helper.IsCellMountains(n.height) && !Helper.IsCellHighMountains(n.height)), map, "desert_01_mask");
        //// desert_mountains
        //await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "desert" && Helper.IsCellMountains(n.height)), map, "mountain_02_desert_mask");
        //// oasis
        //await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) == "oasis"), map, "oasis_mask");
        //// hills
        //await WriteMask(nonWaterProvinceCells.Where(n => Helper.IsCellHills(n.biome, n.height)), map, "hills_01_mask");
        //// low mountains
        //await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellLowMountains(n.height)), map, "mountain_02_mask");
        //// mountains
        //await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellMountains(n.height) || Helper.IsCellHighMountains(n.height)), map, "mountain_02_snow_mask");
        //// HighMountains
        //await WriteMask(nonWaterProvinceCells.Where(n => Helper.MapBiome(n.biome) is "drylands" && Helper.IsCellHighMountains(n.height)), map, "mountain_02_c_snow_mask");
        //// wetlands
        //await WriteMask(provinceBiomes.Where(n => n.Biome == "wetlands").SelectMany(n => n.Province.Cells).Where(n => Helper.MapBiome(n.biome) == "floodplains"), map, "wetlands_02_mask");
    }

}
