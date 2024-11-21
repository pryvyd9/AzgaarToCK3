namespace CK3Tools;

public static class Log
{
    private const string _logDirectoryPath = "logs";
    private static readonly string _logFilePath = Helper.GetPath(_logDirectoryPath, $"LOG_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt");

    //public static void ReadKey()
    //{
    //    Console.ReadKey();
    //    WriteLineFile("\n<WAITING FOR ANY KEY>");
    //}

    //public static string? ReadLine()
    //{
    //    WriteLineFile("\n<WAITING FOR USER INPUT>");
    //    var str = Console.ReadLine();
    //    WriteLineFile("\n[User input] " + str);
    //    return str;
    //}

    public static void Error(object? str)
    {
        WriteLineFile("\n[Error] " + str?.ToString());
    }
    public static void Error(Exception ex, object? str)
    {
        WriteLineFile($"\n[Error] {str}\n{ex.Message}\n{ex.StackTrace}");
    }

    public static void Warning(object? str)
    {
        WriteLineFile("\n[Warning] " + str?.ToString());
    }

    public static void Information(object? str)
    {
        WriteLineFile("\n[Information] " + str?.ToString());
    }

    private static void WriteLineFile(string? str)
    {
        Directory.CreateDirectory(_logDirectoryPath);
        File.AppendAllText(_logFilePath, str);
    }
}

