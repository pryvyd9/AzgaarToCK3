using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Converter;

public static class CK3FileSystem
{
    private static string MyDocuments => Helper.GetPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    public static readonly string defaultModsDirectory = GetDefaultModsDirectory();

    private static string GetDefaultModsDirectory()
    {
        if (OperatingSystem.IsLinux())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Helper.GetPath(home, ".local", "share", "Paradox Interactive", "Crusader Kings III", "mod");
        }
        return Helper.GetPath(MyDocuments, "Paradox Interactive", "Crusader Kings III", "mod");
    }

    private static string FindSteamDirectory()
    {
        if (!Environment.Is64BitOperatingSystem)
        {
            throw new Exception("Only x64 systems are supported.");
        }

        if (OperatingSystem.IsWindows())
        {
            var steamPath = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
               .OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam")
               ?.GetValue("InstallPath") as string
               ?? throw new Exception("Could not find steam InstallPath");

            return steamPath;
        }
        else if (OperatingSystem.IsMacOS())
        {
            var steamPath = Helper.GetPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Steam");

            return steamPath;
        }
        else if (OperatingSystem.IsLinux())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] candidates =
            [
                Helper.GetPath(home, ".local", "share", "Steam"),
                Helper.GetPath(home, ".steam", "steam"),
                Helper.GetPath(home, ".steam", "debian-installation"),
            ];
            foreach (var candidate in candidates)
                if (Directory.Exists(candidate)) return candidate;
            throw new Exception($"Could not find Steam directory. Searched: {string.Join(", ", candidates)}");
        }
        else
        {
            throw new Exception("Operating System not supported");
        }
    }

    private static string GetSteamLibraryFoldersPath()
    {
        return Helper.GetPath(FindSteamDirectory(), "steamapps", "libraryfolders.vdf");
    }

    public static string GetGameDirectory()
    {
        var libraries = File.ReadAllText(GetSteamLibraryFoldersPath());
        var pathRegex = new Regex("\"path\"\\s*\"(.+)\"");
        var paths = pathRegex.Matches(libraries).Select(n => n.Groups[1].Value);

        var ck3Directories = paths.Select(n => Helper.GetPath(n, "steamapps", "common", "Crusader Kings III", "game")).Where(Directory.Exists).ToArray();
        if (ck3Directories.Length > 1)
        {
            Debugger.Break();
            throw new Exception("Multiple game directories found.");
        }
        else if (ck3Directories.Length == 0)
        {
            Debugger.Break();
            throw new Exception("No game directories found.");
        }

        return ck3Directories[0];
    }

    public static string GetModDirectory(string modId)
    {
        var libraries = File.ReadAllText(GetSteamLibraryFoldersPath());
        var pathRegex = new Regex("\"path\"\\s*\"(.+)\"");
        var paths = pathRegex.Matches(libraries).Select(n => n.Groups[1].Value);
        const string ck3SteamId = "1158310";

        var ck3Directories = paths.Select(n => Helper.GetPath(n, "steamapps", "workshop", "content", ck3SteamId, modId)).Where(Directory.Exists).ToArray();
        if (ck3Directories.Length > 1)
        {
            Debugger.Break();
            throw new Exception("Multiple mod directories found.");
        }
        else if (ck3Directories.Length == 0)
        {
            Debugger.Break();
            throw new Exception("No mod directories found.");
        }

        return ck3Directories[0];
    }

    public static string GetTotalConversionSandboxDirectory()
    {
        return GetModDirectory("3595862458");
    }

    public static void ConfigureNumberDecimalSeparator()
    {
        var customCulture = (System.Globalization.CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";
        Thread.CurrentThread.CurrentCulture = customCulture;
    }
}
