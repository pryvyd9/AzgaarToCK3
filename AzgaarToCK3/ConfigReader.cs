using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AzgaarToCK3;

public static class ConfigReader
{
    public record CK3Faith(string name, string[] holySites);
    public record CK3Religion(string name, CK3Faith[] faiths);

    //public record CK3HolySite(string name, bool isLimitedToBarony, Dictionary<string, string> character_modifier);
    public record CK3HolySite(string? barony, Dictionary<string, string> character_modifier, string is_active, string flag);

    // CAUTION: Duplicated keys!
    private static string ToJson(string content)
    {
        // Remove comments
        content = Regex.Replace(content, @"#.*[\s\n]", "");
        // remove hsv color modifier
        content = Regex.Replace(content, "hsv", "");
        // "key":"value"
        content = Regex.Replace(content, @"([\w-]+)\s*=", "\"$1\":");
        content = Regex.Replace(content, @":\s*([\w\.-]+)", ":\"$1\"");
        // array
        content = Regex.Replace(content, @"{[\s\n]*([\s\n\w\d\.-]+)[\s\n]*}", "[$1]");
        content = Regex.Replace(content, @"\[\s*([\w\d])", "[\"$1");
        content = Regex.Replace(content, @"([\w\d])[\s\n]+([\w\d])", "$1\",\"$2");
        content = Regex.Replace(content, @"([\w\d])[\s\n]*]", "$1\"]");
        // object array
        content = Regex.Replace(content, """{[\s\n]*(([\s\n]*{[\s\n\w=":]*})*)[\s\n]*}""", "[$1]");
        // fill missing commas
        content = Regex.Replace(content, """(["}\]])[\s\n]*(["{])""", "$1,$2");

        return "{" + content + "}";
    }

    // CK3 file structure is tricky and some things cannot be parsed yet.
    // Unsupported religions: west_african_religion
    public static async Task<List<CK3Religion>> GetCK3Religions(Settings settings)
    {
        var religions = new List<CK3Religion>();

        var religionsPath = $"{settings.ck3Directory}\\common\\religion\\religions";
        var religionFiles = Directory.EnumerateFiles(religionsPath).Where(n => n.EndsWith(".txt"));

        foreach (var religionFilename in religionFiles)
        {
            var content = await File.ReadAllTextAsync(religionFilename);
            var contentJson = ToJson(content);
            try
            {
                // faith name, holy sites
                var religion = JsonSerializer.Deserialize<Dictionary<string, JsonDocument>>(contentJson, new JsonSerializerOptions { })!.First();

                var h = religion.Value.RootElement.EnumerateObject()
                .GroupBy(n => n.Name, n => n.Value)
                .ToDictionary(n => n.Key, n => n.Select(m => m));

                var faiths = h["faiths"]
                    .SelectMany(n => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(n)!.Select(m => m))
                    .ToDictionary(n => n.Key, n => n.Value);

                var ck3Faiths = faiths
                    .SelectMany(f => f.Value.EnumerateObject().Where(n => n.Name == "holy_site").Select(n => (f.Key, n.Value.GetString())))
                    .GroupBy(n => n.Key, n => n.Item2)
                    .Select(n => new CK3Faith(n.Key, n.ToArray()!))
                    .ToArray();

                religions.Add(new CK3Religion(religion.Key, ck3Faiths));
            }
            catch(Exception ex)
            {
                // ignore errors (west_african_religion)
                continue;
            }
        }
       
        return religions;
    }
    public static async Task<Dictionary<string, CK3HolySite>> GetCK3HolySites(Settings settings)
    {
        var holySitesPath = @$"{settings.ck3Directory}\common\religion\holy_sites\00_holy_sites.txt";
        var file = await File.ReadAllTextAsync(holySitesPath);
        var json = ToJson(file);
        try
        {
            var holySites = JsonSerializer.Deserialize<Dictionary<string, CK3HolySite>>(json);

            return holySites;
        }catch(Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }
}
