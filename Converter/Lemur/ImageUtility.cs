using System.Diagnostics;
using ImageMagick;

namespace Converter.Lemur
{
    public static class ImageUtility
    {
        public static void OpenImageInExplorer(string path)
        {
            //Console.WriteLine("Debug is on, opening the image...");
            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                ArgumentList = { path },
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        public static async Task DrawCells(Entities.Map map)
        {
            try
            {
                var settings = new MagickReadSettings()
                {
                    Width = Map.MapWidth,
                    Height = Map.MapHeight,
                };
                using var cellsMap = new MagickImage("xc:white", settings);

                var drawables = new Drawables();
                foreach (var feature in map.GeoMap.features)
                {
                    foreach (var cell in feature.geometry.coordinates)
                    {
                        drawables
                            .DisableStrokeAntialias()
                            .StrokeWidth(2)
                            .StrokeColor(MagickColors.Black)
                            .FillOpacity(new Percentage(0))
                            .Polygon(cell.Select(n => Helper.GeoToPixel(n[0], n[1], map)));
                    }
                }

                cellsMap.Draw(drawables);
                string path = Converter.Helper.GetPath($"{Environment.CurrentDirectory}/cells.png");
                await cellsMap.WriteAsync(path);

                if (Settings.Instance.Debug)
                {
                    Console.WriteLine("Debug is on, opening the image...");
                    OpenImageInExplorer(path);
                }
            }
            catch (Exception ex)
            {
                Debugger.Break();
                throw;
            }


        }
    }


}