using Converter;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace GraphicalUI;

public partial class ConversionPage : ContentPage
{

	public ConversionPage(string exportedDoc)
	{
		InitializeComponent();

        MyConsole.Sink = WriteLine;

        _ = Task.Run(async () =>
        {
            try
            {
                var unescapedDoc = await UnescapeDoc(exportedDoc);

                var xml = new XmlDocument();
                xml.LoadXml(unescapedDoc);

                var fileNameAttribute = (xml.FirstChild as XmlElement).GetAttribute("fileName");
                var filename = $"{fileNameAttribute}.xml";
                xml.Save(filename);

                MyConsole.WriteLine("Export completed. Starting Conversion");

                Settings.Instance.InputXmlPath = filename;
                await ModManager.Run();

                MyConsole.WriteLine("Conversion Finished. Enjoy!");
                await MainThread.InvokeOnMainThreadAsync(() => okButton.IsEnabled = true);
            } catch (Exception ex)
            {
                MyConsole.Error(ex, "Failed to export");
            }
          
        });
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

    public void WriteLine(object? str)
	{
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                log.Text += $"{str}\n";
            }
            catch (Exception ex)
            {
                Debugger.Break();
            }
        });
	}

    private async void okButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}