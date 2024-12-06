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

        if (!File.Exists(Settings.Instance.InputXmlPath))
        {
            MyConsole.Info($".xml file has not been found.");
            MyConsole.Info($"Please, place it in '{Settings.Instance.InputXmlPath}' or change '{nameof(Settings.Instance.InputXmlPath)}' in 'settings.json'.");
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
        if (ModManager.FindLatestInputs() is { } xmlName && xmlName != Settings.Instance.InputXmlPath)
        {
            MyConsole.Info("Found new input in the directory:");
            MyConsole.Info(Path.GetFileName(xmlName));
            MyConsole.Info("Use it as input?");

            if (YesNo())
            {
                Settings.Instance.InputXmlPath = xmlName;
            }
            else
            {
                EnsureInputsExist();

                MyConsole.Info("Previously used input will be used:");
                MyConsole.Info(Settings.Instance.InputXmlPath);
            }
        }
        else
        {
            EnsureInputsExist();
        }

        // Exit if inputs not found
        static void EnsureInputsExist()
        {
            var xmlExists = File.Exists(Settings.Instance.InputXmlPath);

            if (!xmlExists)
            {
                MyConsole.Warning(".xml input was not found.");
            }
            

            if (!xmlExists)
            {
                MyConsole.Info($"-------------------------------------------------");
                MyConsole.Info($"Put your exported .xml file to this app's folder ({SettingsManager.ExecutablePath}).");
                MyConsole.Info("Make sure it has the latest 'modification date'.");
                MyConsole.Info("If the wrong files are found delete other exported .xml files from the folder.");
                MyConsole.Info("If the files cannot be found open 'settings.json' and modify 'InputXmlPath' value to point to your file.");
                MyConsole.Info($"-------------------------------------------------");

                Exit();
            }
        }
    }


}
