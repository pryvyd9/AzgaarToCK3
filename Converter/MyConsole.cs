using SixLabors.ImageSharp.Drawing;

namespace Converter;

public static class MyConsole
{
    private const string _logDirectoryPath = "logs";
    private static readonly string _logFilePath = Helper.GetPath(_logDirectoryPath, $"LOG_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt");

    public static Action<object?> Sink = Console.WriteLine;

    public static void WriteLine()
    {
        Sink(null);
        WriteLineFile("\n");
    }

    public static void WriteLine(object? str)
    {
        Sink(str);
        WriteLineFile("\n" + str?.ToString());
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
        Sink(str);
        WriteLineFile("\n[Error] " + str?.ToString());
    }
    public static void Error(Exception ex, object? str)
    {
        Sink(str);
        WriteLineFile($"\n[Error] {ex.Message}\n{ex.StackTrace}\n{str}");
    }

    public static void Warning(object? str)
    {
        Sink(str);
        WriteLineFile("\n[Warning] " + str?.ToString());
    }

    private static void WriteLineFile(string? str)
    {
        Directory.CreateDirectory(_logDirectoryPath);
        File.AppendAllText(_logFilePath, str);
    }
}

