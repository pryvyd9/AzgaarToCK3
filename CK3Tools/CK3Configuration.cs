namespace CK3Tools;

public static class CK3Configuration
{
    public static void ConfigureNumberDecimalSeparator()
    {
        var customCulture = (System.Globalization.CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ".";
        Thread.CurrentThread.CurrentCulture = customCulture;
    }
}
