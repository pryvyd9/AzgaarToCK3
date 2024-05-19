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

        foreach (var property in Settings.Instance.GetType().GetProperties())
        {
            Console.WriteLine($"{property.Name,-30}: {property.GetValue(Settings.Instance)}");
        }

        // Configure NumberDecimalSeparator. Writing files will not work otherwise.
        SettingsManager.Configure();

        Console.WriteLine();
        Console.WriteLine("The app has been configured. Feel free to change the settings in 'settings.json' file.");
        Console.WriteLine();

        if (string.IsNullOrWhiteSpace(Settings.Instance.modName))
        {
            Console.Write("Name your mod: ");
            Settings.Instance.modName = Console.ReadLine()!;
        }

        CheckIfShouldOverride();
        TryFindInputs();

        if (!File.Exists(Settings.Instance.inputJsonPath))
        {
            Console.WriteLine($".json file has not been found.");
            Console.WriteLine($"Please, place it in '{Settings.Instance.inputJsonPath}' or change '{nameof(Settings.Instance.inputJsonPath)}' in 'settings.json'.");
            Exit();
        }
        if (!File.Exists(Settings.Instance.inputGeojsonPath))
        {
            Console.WriteLine($".geojson file has not been found.");
            Console.WriteLine($"Please, place it in '{Settings.Instance.inputGeojsonPath}' or change '{nameof(Settings.Instance.inputGeojsonPath)}' in 'settings.json'.");
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
            if (Settings.Instance.shouldOverride is not null)
            {
                break;
            }

            Console.WriteLine("Mod already exists. Override?");
            Settings.Instance.shouldOverride = YesNo();
            if (!Settings.Instance.shouldOverride.Value)
            {
                Console.WriteLine("ChangeModName?");
                if (!YesNo())
                {
                    Console.WriteLine("Exiting... Please, change mod name in 'settings.json' if needed and try again");
                    Exit();
                }
                Console.WriteLine("Name your mod:");
                Settings.Instance.modName = Console.ReadLine()!;
                break;
            }
            else
            {
                break;
            }
        }

        if (Settings.Instance.shouldOverride ?? false)
        {
            Console.WriteLine($"Mod will be overriden in all future runs. If you wish to change it change '{nameof(Settings.Instance.shouldOverride)}' in 'settings.json' file.");
        }

    }

    private static void TryFindInputs()
    {
        if (ModManager.TryFindInputs() is ({ } jsonName, { } geojsonName) &&
          (Settings.Instance.inputJsonPath != jsonName || Settings.Instance.inputGeojsonPath != geojsonName))
        {
            Console.WriteLine($"Found 2 files in the directory: ");
            Console.WriteLine(Path.GetFileName(jsonName));
            Console.WriteLine(Path.GetFileName(geojsonName));
            Console.WriteLine("Use them as inputs?");

            if (YesNo())
            {
                Settings.Instance.inputJsonPath = jsonName;
                Settings.Instance.inputGeojsonPath = geojsonName;
            }
        }
    }

}
