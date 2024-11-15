namespace Converter;

public static class MyConsole
{
    private const string _logDirectoryPath = "logs";
    private static readonly string _logFilePath = Helper.GetPath(_logDirectoryPath, $"LOG_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt");

    public static void WriteLine()
    {
        Console.WriteLine();
        WriteLineFile("\n");
    }

    public static void WriteLine(object? str)
    {
        Console.WriteLine(str);
        WriteLineFile("\n" + str?.ToString());
    }

    public static void Write(object? str)
    {
        Console.Write(str);
        WriteLineFile(str?.ToString());
    }

    public static void ReadKey()
    {
        Console.ReadKey();
        WriteLineFile("\n<WAITING FOR ANY KEY>");
    }

    public static string? ReadLine()
    {
        WriteLineFile("\n<WAITING FOR USER INPUT>");
        var str = Console.ReadLine();
        WriteLineFile("\n[User input] " + str);
        return str;
    }

    public static void Error(object? str)
    {
        Console.WriteLine(str);
        WriteLineFile("\n[Error] " + str?.ToString());
    }

    public static void Warning(object? str)
    {
        Console.WriteLine(str);
        WriteLineFile("\n[Warning] " + str?.ToString());
    }

    private static void WriteLineFile(string? str)
    {
        Directory.CreateDirectory(_logDirectoryPath);
        File.AppendAllText(_logFilePath, str);
    }
}

