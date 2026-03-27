using Converter;
using System.Diagnostics;

namespace ConsoleUI;

internal class Program
{
    static async Task Run()
    {
        if (!SettingsManager.TryLoad())
        {
            SettingsManager.CreateDefault();
            MyConsole.Info("Default Settings file has been created.");
        }

        // Print settings
        MyConsole.Info(Settings.Instance);

        // Configure NumberDecimalSeparator. Writing files will not work otherwise.
        SettingsManager.Configure();

        MyConsole.Info();
        MyConsole.Info("The app has been configured. Feel free to change the settings in 'settings.json' file.");
        MyConsole.Info("Check https://github.com/pryvyd9/AzgaarToCK3 for instructions or feedback.");
        MyConsole.Info();

        if (string.IsNullOrWhiteSpace(Settings.Instance.ModName))
        {
            MyConsole.Info("Name your mod: ");
            Settings.Instance.ModName = MyConsole.ReadLine()!;
        }

        CheckIfShouldOverride();
        FindInputs();

        if (!File.Exists(Settings.Instance.InputJsonPath) || !File.Exists(Settings.Instance.InputGeojsonPath))
        {
            if (!File.Exists(Settings.Instance.InputJsonPath))
            {
                MyConsole.Info($".json file has not been found.");
                MyConsole.Info($"Please, place it in '{Settings.Instance.InputJsonPath}' or change '{nameof(Settings.Instance.InputJsonPath)}' in 'settings.json'.");
            }
            if (!File.Exists(Settings.Instance.InputGeojsonPath))
            {
                MyConsole.Info($".geojson file has not been found.");
                MyConsole.Info($"Please, place it in '{Settings.Instance.InputGeojsonPath}' or change '{nameof(Settings.Instance.InputGeojsonPath)}' in 'settings.json'.");
            }
            Exit();
        }

        MyConsole.Info("Start conversion?");
        if (YesNo())
        {
            // Copy sandbox mod files.
            if (!ModManager.DoesModExist())
            {
                await ModManager.CreateMod();
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                await ModManager.Run();
                stopwatch.Stop();
                MyConsole.Info("Map conversion finished successfully.");
                MyConsole.Info("Add newly created mod to your playset and enjoy!");
                MyConsole.Info($"[Conversion time]: {stopwatch.Elapsed.TotalSeconds}s", true);
            }
            catch (Exception ex)
            {
                MyConsole.Error("An error has occured.");
                MyConsole.Error(ex.Message);
                MyConsole.Error(ex.StackTrace);
            }
        }

#if DEBUG
        SettingsManager.Save();
        Environment.Exit(0);
#endif

    

        Exit();
    }
    static async Task Main(string[] args)
    {
        try
        {
            await Run();
        }catch(Exception ex)
        {
            MyConsole.Error("An error has occured.");
            MyConsole.Error(ex.Message);
            MyConsole.Error(ex.StackTrace);
        }
    }

    private static bool YesNo()
    {
        int maxTries = 10;
        for (int i = 0; i < maxTries; i++)
        {
            MyConsole.Info("1. Yes.");
            MyConsole.Info("2. No.");
            var response = MyConsole.ReadLine()!;
            if (response == "1")
            {
                return true;
            }
            else if (response == "2")
            {
                return false;
            }
        }
        MyConsole.Info("Failed to read supported response.");
        Exit();
        return false;
    }

    private static void Exit()
    {
        MyConsole.Info("Press any key to exit.");
        MyConsole.ReadKey();
        SettingsManager.Save();
        Environment.Exit(0);
    }
    
    private static void CheckIfShouldOverride()
    {
        while (ModManager.DoesModExist())
        {
            if (Settings.Instance.ShouldOverride is not null)
            {
                break;
            }

            MyConsole.Info("Mod already exists. Override?");
            Settings.Instance.ShouldOverride = YesNo();
            if (!Settings.Instance.ShouldOverride.Value)
            {
                MyConsole.Info("ChangeModName?");
                if (!YesNo())
                {
                    MyConsole.Info("Exiting... Please, change mod name in 'settings.json' if needed and try again");
                    Exit();
                }
                MyConsole.Info("Name your mod:");
                Settings.Instance.ModName = MyConsole.ReadLine()!;
                break;
            }
            else
            {
                break;
            }
        }

        if (Settings.Instance.ShouldOverride ?? false)
        {
            MyConsole.Info($"Mod will be overriden in all future runs. If you wish to change it change '{nameof(Settings.Instance.ShouldOverride)}' in 'settings.json' file.");
        }

    }

    private static void FindInputs()
    {
        var (foundJson, foundGeojson) = ModManager.FindLatestInputs();

        bool jsonUpdated = false;
        bool geojsonUpdated = false;

        if (foundJson != null && foundJson != Settings.Instance.InputJsonPath)
        {
            MyConsole.Info("Found new .json input in the directory:");
            MyConsole.Info(Path.GetFileName(foundJson));
            MyConsole.Info("Use it as input?");
            if (YesNo())
            {
                Settings.Instance.InputJsonPath = foundJson;
                jsonUpdated = true;
            }
        }

        if (foundGeojson != null && foundGeojson != Settings.Instance.InputGeojsonPath)
        {
            MyConsole.Info("Found new .geojson input in the directory:");
            MyConsole.Info(Path.GetFileName(foundGeojson));
            MyConsole.Info("Use it as input?");
            if (YesNo())
            {
                Settings.Instance.InputGeojsonPath = foundGeojson;
                geojsonUpdated = true;
            }
        }

        if (!jsonUpdated || !geojsonUpdated)
        {
            EnsureInputsExist();
        }

        // Exit if inputs not found
        static void EnsureInputsExist()
        {
            var jsonExists = File.Exists(Settings.Instance.InputJsonPath);
            var geojsonExists = File.Exists(Settings.Instance.InputGeojsonPath);

            if (!jsonExists)
            {
                MyConsole.Warning(".json input was not found.");
            }
            if (!geojsonExists)
            {
                MyConsole.Warning(".geojson input was not found.");
            }

            if (!jsonExists || !geojsonExists)
            {
                MyConsole.Info($"-------------------------------------------------");
                MyConsole.Info($"Export your map from Azgaar's Fantasy Map Generator:");
                MyConsole.Info($"  File -> Export -> JSON  -> save as .json");
                MyConsole.Info($"  File -> Export -> GeoJSON -> save as .geojson");
                MyConsole.Info($"Put both files in this app's folder ({SettingsManager.ExecutablePath}).");
                MyConsole.Info("Make sure they have the latest 'modification date'.");
                MyConsole.Info("Or open 'settings.json' and set 'InputJsonPath' / 'InputGeojsonPath' directly.");
                MyConsole.Info($"-------------------------------------------------");

                Exit();
            }
        }
    }


}
