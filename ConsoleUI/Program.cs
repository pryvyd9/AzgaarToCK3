using Converter;

namespace ConsoleUI;

internal class Program
{
    static async Task Run()
    {
        if (!SettingsManager.TryLoad())
        {
            SettingsManager.CreateDefault();
            MyConsole.WriteLine("Default Settings file has been created.");
        }

        // Print settings
        MyConsole.WriteLine(Settings.Instance);

        // Configure NumberDecimalSeparator. Writing files will not work otherwise.
        SettingsManager.Configure();

        MyConsole.WriteLine();
        MyConsole.WriteLine("The app has been configured. Feel free to change the settings in 'settings.json' file.");
        MyConsole.WriteLine("Check https://github.com/pryvyd9/AzgaarToCK3 for instructions or feedback.");
        MyConsole.WriteLine();

        if (string.IsNullOrWhiteSpace(Settings.Instance.ModName))
        {
            MyConsole.Write("Name your mod: ");
            Settings.Instance.ModName = MyConsole.ReadLine()!;
        }

        CheckIfShouldOverride();
        FindInputs();

        if (!File.Exists(Settings.Instance.InputJsonPath))
        {
            MyConsole.WriteLine($".json file has not been found.");
            MyConsole.WriteLine($"Please, place it in '{Settings.Instance.InputJsonPath}' or change '{nameof(Settings.Instance.InputJsonPath)}' in 'settings.json'.");
            Exit();
        }
        if (!File.Exists(Settings.Instance.InputGeojsonPath))
        {
            MyConsole.WriteLine($".geojson file has not been found.");
            MyConsole.WriteLine($"Please, place it in '{Settings.Instance.InputGeojsonPath}' or change '{nameof(Settings.Instance.InputGeojsonPath)}' in 'settings.json'.");
            Exit();
        }
        if (!File.Exists(Settings.Instance.InputMapPath))
        {
            MyConsole.WriteLine($".map file has not been found.");
            MyConsole.WriteLine($"Please, place it in '{Settings.Instance.InputMapPath}' or change '{nameof(Settings.Instance.InputGeojsonPath)}' in 'settings.json'.");
            Exit();
        }

        MyConsole.WriteLine("Start conversion?");
        if (YesNo())
        {
            // Copy sandbox mod files.
            if (!ModManager.DoesModExist())
            {
                await ModManager.CreateMod();
            }

            try
            {
                await ModManager.Run();
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

        MyConsole.WriteLine("Map conversion finished successfully.");
        MyConsole.WriteLine("Add newly created mod to your playset and enjoy!");

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
            MyConsole.WriteLine("1. Yes.");
            MyConsole.WriteLine("2. No.");
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
        MyConsole.WriteLine("Failed to read supported response.");
        Exit();
        return false;
    }

    private static void Exit()
    {
        MyConsole.WriteLine("Press any key to exit.");
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

            MyConsole.WriteLine("Mod already exists. Override?");
            Settings.Instance.ShouldOverride = YesNo();
            if (!Settings.Instance.ShouldOverride.Value)
            {
                MyConsole.WriteLine("ChangeModName?");
                if (!YesNo())
                {
                    MyConsole.WriteLine("Exiting... Please, change mod name in 'settings.json' if needed and try again");
                    Exit();
                }
                MyConsole.WriteLine("Name your mod:");
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
            MyConsole.WriteLine($"Mod will be overriden in all future runs. If you wish to change it change '{nameof(Settings.Instance.ShouldOverride)}' in 'settings.json' file.");
        }

    }

    private static void FindInputs()
    {
        if (ModManager.FindLatestInputs() is ({ } jsonName, { } geojsonName, { } mapName) && 
            (jsonName != Settings.Instance.InputJsonPath || geojsonName != Settings.Instance.InputGeojsonPath || mapName != Settings.Instance.InputMapPath))
        {
            MyConsole.WriteLine("Found new inputs in the directory:");
            MyConsole.WriteLine(Path.GetFileName(jsonName));
            MyConsole.WriteLine(Path.GetFileName(geojsonName));
            MyConsole.WriteLine(Path.GetFileName(mapName));
            MyConsole.WriteLine("Use them as inputs?");

            if (YesNo())
            {
                Settings.Instance.InputJsonPath = jsonName;
                Settings.Instance.InputGeojsonPath = geojsonName;
                Settings.Instance.InputMapPath = mapName;
            }
            else
            {
                EnsureInputsExist();

                MyConsole.WriteLine("Previously used inputs will be used:");
                MyConsole.WriteLine(Settings.Instance.InputJsonPath);
                MyConsole.WriteLine(Settings.Instance.InputGeojsonPath);
                MyConsole.WriteLine(Settings.Instance.InputMapPath);
            }
        }
        else
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
                MyConsole.WriteLine($"-------------------------------------------------");
                MyConsole.WriteLine($"Put your exported .json, .geojson files to this app's folder ({SettingsManager.ExecutablePath}).");
                MyConsole.WriteLine("Make sure they are they have the latest 'modification date'.");
                MyConsole.WriteLine("If the wrong files are found delete other exported .json, .geojson files from the folder.");
                MyConsole.WriteLine("If the files cannot be found open 'settings.json' and modify 'InputJsonPath' and 'InputGeojsonPath' values to point to your files.");
                MyConsole.WriteLine($"-------------------------------------------------");

                Exit();
            }
        }
    }

}
