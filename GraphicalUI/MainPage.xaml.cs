using Converter;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace GraphicalUI;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        if (!SettingsManager.TryLoad())
        {
            SettingsManager.CreateDefault();
            Log.Information("Default Settings file has been created.");
        }

        InitializeComponent();
    }

    private void loadButton_Clicked(object sender, EventArgs e)
    {

    }

    private async void createModButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushModalAsync(new CreateNewModPage());
    }

    private async Task<string> UnescapeDoc(string doc)
    {

        // Unescape that damn Unicode Java bull.
        var unescapedDoc = Regex.Replace(doc, @"\\[Uu]([0-9A-Fa-f]{4})", m => char.ToString((char)ushort.Parse(m.Groups[1].Value, NumberStyles.AllowHexSpecifier)));
        unescapedDoc = Regex.Unescape(unescapedDoc);

        string RemoveEmoji(string str)
        {
            bool IsLegalXmlChar(char character)
            {
                return
                (
                     character == 0x9 /* == '\t' == 9   */          ||
                     character == 0xA /* == '\n' == 10  */          ||
                     character == 0xD /* == '\r' == 13  */          ||
                    (character >= 0x20 && character <= 0xD7FF) ||
                    (character >= 0xE000 && character <= 0xFFFD) ||
                    (character >= 0x10000 && character <= 0x10FFFF)
                );
            }
            return new string(str.Select(n => IsLegalXmlChar(n) ? n : 'X').ToArray());
        }

        unescapedDoc = RemoveEmoji(unescapedDoc);
        unescapedDoc = unescapedDoc.Replace("&amp;quot;", "\"");
        unescapedDoc = unescapedDoc.Replace("xmlns=\"http://www.w3.org/1999/xhtml\"", "");

        return unescapedDoc;
    }

    private async Task<string> ExportCK3()
    {
        // There's no way to run async js and await for it.
        // Instead, we start a promise and will wait until it assigns
        // its result to document.
        _ = await webView.EvaluateJavaScriptAsync("document.prepareCK3();");
        
        string? doc = null;
        while (doc is null)
        {
            await Task.Delay(1000);
            doc = await webView.EvaluateJavaScriptAsync("document.serializedMap");
        };

        return doc;
    }

    private async void populateModButton_Clicked(object sender, EventArgs e)
    {
        MyConsole.WriteLine("Starting export...");
        var doc = await ExportCK3();

        await Navigation.PushModalAsync(new ConversionPage(doc));

        //_ = Task.Run(async () =>
        //{
        //    var doc = await ExportCK3();
        //    var unescapedDoc = await UnescapeDoc(doc);

        //    var xml = new XmlDocument();
        //    xml.LoadXml(unescapedDoc);

        //    var fileNameAttribute = (xml.FirstChild as XmlElement).GetAttribute("fileName");
        //    var filename = $"{fileNameAttribute}.xml";
        //    xml.Save(filename);

        //    Settings.Instance.InputXmlPath = filename;
        //    await ModManager.Run();
        //});
    }

    private void saveButton_Clicked(object sender, EventArgs e)
    {

    }

    private async void settingsButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushModalAsync(new SettingsPage());
    }
}

