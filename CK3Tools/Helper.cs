namespace CK3Tools;

public static class Helper
{
    // Generates path that works in both Windows and Mac
    public static string GetPath(params string[] paths)
    {
        if (paths is null) return "";
        try
        {
            return Path.Combine(paths.SelectMany(n => n.Replace(@"\\", "/").Replace(@"\", "/").Split("/")).ToArray());
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to combine paths: {string.Join(",", paths)}", ex);
        }
    }
}
