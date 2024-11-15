using Converter;

namespace ConsoleUI;

internal class Program
{
    static async Task Run()
    {
        if (!SettingsManager.TryLoad())
        {
            SettingsManager.CreateDefault();
            Console.WriteLine("Default Settings file has been created.");
        }

        // Print settings
        Console.WriteLine(Settings.Instance);

        // Configure NumberDecimalSeparator. Writing files will not work otherwise.
        SettingsManager.Configure();

        Console.WriteLine();
        Console.WriteLine("The app has been configured. Feel free to change the settings in 'settings.json' file.");
        Console.WriteLine("Check https://github.com/pryvyd9/AzgaarToCK3 for instructions or feedback.");
        Console.WriteLine();

        if (string.IsNullOrWhiteSpace(Settings.Instance.ModName))
        {
            Console.Write("Name your mod: ");
            Settings.Instance.ModName = Console.ReadLine()!;
        }

        CheckIfShouldOverride();
        FindInputs();

        if (!File.Exists(Settings.Instance.InputJsonPath))
        {
            Console.WriteLine($".json file has not been found.");
            Console.WriteLine($"Please, place it in '{Settings.Instance.InputJsonPath}' or change '{nameof(Settings.Instance.InputJsonPath)}' in 'settings.json'.");
            Exit();
        }
        if (!File.Exists(Settings.Instance.InputGeojsonPath))
        {
            Console.WriteLine($".geojson file has not been found.");
            Console.WriteLine($"Please, place it in '{Settings.Instance.InputGeojsonPath}' or change '{nameof(Settings.Instance.InputGeojsonPath)}' in 'settings.json'.");
            Exit();
        }

        Console.WriteLine("Start conversion?");
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
                Console.WriteLine("An error has occured.");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

#if DEBUG
        SettingsManager.Save();
        Environment.Exit(0);
#endif

        Console.WriteLine("Map conversion finished successfully.");
        Console.WriteLine("Add newly created mod to your playset and enjoy!");

        Exit();
    }
    static async Task Main(string[] args)
    {
        try
        {
            await Run();
        }catch(Exception ex)
        {
            Console.WriteLine("An error has occured.");
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static bool YesNo()
    {
        int maxTries = 10;
        string response = "";
        for (int i = 0; i < maxTries; i++)
        {
            Console.WriteLine("1. Yes.");
            Console.WriteLine("2. No.");
            response = Console.ReadLine()!;
            if (response == "1")
            {
                return true;
            }
            else if (response == "2")
            {
                return false;
            }
        }
        Console.WriteLine("Failed to read supported response.");
        Exit();
        return false;
    }

    private static void Exit()
    {
        Console.WriteLine("Press any key to exit.");
        Console.ReadKey();
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

            Console.WriteLine("Mod already exists. Override?");
            Settings.Instance.ShouldOverride = YesNo();
            if (!Settings.Instance.ShouldOverride.Value)
            {
                Console.WriteLine("ChangeModName?");
                if (!YesNo())
                {
                    Console.WriteLine("Exiting... Please, change mod name in 'settings.json' if needed and try again");
                    Exit();
                }
                Console.WriteLine("Name your mod:");
                Settings.Instance.ModName = Console.ReadLine()!;
                break;
            }
            else
            {
                break;
            }
        }

        if (Settings.Instance.ShouldOverride ?? false)
        {
            Console.WriteLine($"Mod will be overriden in all future runs. If you wish to change it change '{nameof(Settings.Instance.ShouldOverride)}' in 'settings.json' file.");
        }

    }

    private static void FindInputs()
    {
        if (ModManager.FindLatestInputs() is ({ } jsonName, { } geojsonName) && 
            (jsonName != Settings.Instance.InputJsonPath || geojsonName != Settings.Instance.InputGeojsonPath))
        {
            Console.WriteLine("Found new inputs in the directory:");
            Console.WriteLine(Path.GetFileName(jsonName));
            Console.WriteLine(Path.GetFileName(geojsonName));
            Console.WriteLine("Use them as inputs?");

            if (YesNo())
            {
                Settings.Instance.InputJsonPath = jsonName;
                Settings.Instance.InputGeojsonPath = geojsonName;
            }
            else
            {
                EnsureInputsExist();

                Console.WriteLine("Previously used inputs will be used:");
                Console.WriteLine(Settings.Instance.InputJsonPath);
                Console.WriteLine(Settings.Instance.InputGeojsonPath);
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
                Console.WriteLine(".json input was not found.");
            }
            if (!geojsonExists)
            {
                Console.WriteLine(".geojson input was not found.");
            }

            if (!jsonExists || !geojsonExists)
            {
                Console.WriteLine($"-------------------------------------------------");
                Console.WriteLine($"Put your exported .json, .geojson files to this app's folder ({SettingsManager.ExecutablePath}).");
                Console.WriteLine("Make sure they are they have the latest 'modification date'.");
                Console.WriteLine("If the wrong files are found delete other exported .json, .geojson files from the folder.");
                Console.WriteLine("If the files cannot be found open 'settings.json' and modify 'InputJsonPath' and 'InputGeojsonPath' values to point to your files.");
                Console.WriteLine($"-------------------------------------------------");

                Exit();
            }
        }
    }

}
