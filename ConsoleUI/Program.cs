using Converter;
using static System.Net.Mime.MediaTypeNames;

namespace ConsoleUI;

internal class Program
{
    static async Task Main(string[] args)
    {
        if (!SettingsManager.TryLoad())
        {
            SettingsManager.CreateDefault();
            Console.WriteLine("Default Settings file has been created.");
        }

        foreach (var property in SettingsManager.Settings.GetType().GetProperties())
        {
            Console.WriteLine($"{property.Name,-30}: {property.GetValue(SettingsManager.Settings)}");
        }

        // Configure NumberDecimalSeparator. Writing files will not work otherwise.
        SettingsManager.Configure();

        Console.WriteLine();
        Console.WriteLine("The app has been configured. Feel free to change the settings in 'settings.json' file.");
        Console.WriteLine();

        if (string.IsNullOrWhiteSpace(SettingsManager.Settings.modName))
        {
            Console.Write("Name your mod: ");
            SettingsManager.Settings.modName = Console.ReadLine()!;
        }

        while (ModManager.DoesModExist())
        {
            if (SettingsManager.Settings.shouldOverride is not null)
            {
                break;
            }

            Console.WriteLine("Mod already exists. Override?");
            SettingsManager.Settings.shouldOverride = YesNo();
            if (!SettingsManager.Settings.shouldOverride.Value)
            {
                Console.WriteLine("ChangeModName?");
                if (!YesNo())
                {
                    Console.WriteLine("Exiting... Please, change mod name in 'settings.json' if needed and try again");
                    Exit();
                }
                Console.WriteLine("Name your mod:");
                SettingsManager.Settings.modName = Console.ReadLine()!;
                break;
            }
            else
            {
                break;
            }
        }

        if (SettingsManager.Settings.shouldOverride!.Value)
        {
            Console.WriteLine($"Mod will be overriden in all future runs. If you wish to change it change '{nameof(SettingsManager.Settings.shouldOverride)}' in 'settings.json' file.");
        }

        if (!File.Exists(SettingsManager.Settings.inputJsonPath))
        {
            Console.WriteLine($".json file has not been found.");
            Console.WriteLine($"Please, place it in '{SettingsManager.Settings.inputJsonPath}' or change '{nameof(SettingsManager.Settings.inputJsonPath)}' in 'settings.json'.");
            Exit();
        }
        if (!File.Exists(SettingsManager.Settings.inputGeojsonPath))
        {
            Console.WriteLine($".geojson file has not been found.");
            Console.WriteLine($"Please, place it in '{SettingsManager.Settings.inputGeojsonPath}' or change '{nameof(SettingsManager.Settings.inputGeojsonPath)}' in 'settings.json'.");
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

        Exit();
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
    //private static int GetResponse(params string[] options)
    //{
    //    for (int i = 0; i < options.Length; i++)
    //    {
    //        Console.WriteLine($"{i,-2}. {options[i]}.");
    //    }

    //    return Console.Read();
    //}
    //private static void Act(params KeyValuePair<string, Action>[] options)
    //{
    //    for (int i = 0; i < options.Length; i++)
    //    {
    //        Console.WriteLine($"{i,-2}. {options[i].Key}.");
    //    }

    //    var response = Console.Read();
    //    options[response].Value();
    //}

    //private static void Act(params (string, Action)[] options)
    //{
    //    for (int i = 0; i < options.Length; i++)
    //    {
    //        Console.WriteLine($"{i,-2}. {options[i].Item1}.");
    //    }

    //    var response = Console.Read();
    //    options[response].Item2();
    //}

}
