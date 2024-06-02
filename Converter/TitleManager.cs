using SixLabors.ImageSharp;
using System.Diagnostics;
using System.Text;

namespace Converter;

public static class TitleManager
{
    private static List<Duchy> CreateDuchies(Map map)
    {
        try
        {
            var duchies = new List<Duchy>();
            var processedProvinces = new HashSet<Province>();
            var bi = 1;
            var ci = 1;
            var di = 1;

            foreach (var state in map.JsonMap.pack.states.Where(n => n.provinces.Any()))
            {
                // Some of the provinces were deleted since they were empty or too small.
                // Skip those deleted provinces.
                var provinces = state.provinces.Select(n => map.Provinces.FirstOrDefault(m => m.Id == n)).Where(n => n is not null && !n.IsWater).ToArray();

                // Each county should have 4 or fewer counties.
                var countyCount = state.provinces.Length / 4;
                var unprocessedProvinces = provinces.Except(processedProvinces).ToHashSet();
                var counties = new List<County>();

                if (provinces is [])
                {
                    // Skip states without provinces.
                    continue;
                }
                var currentProvince = provinces[0];

                do
                {
                    for (int i = 0; i < 4 && !processedProvinces.Contains(currentProvince); i++)
                    {
                        if (i == 0)
                        {
                            counties.Add(new County()
                            {
                                id = ci++,
                                Color = currentProvince.Color,
                                CapitalName = currentProvince.Name,
                                Name = "County of " + currentProvince.Name,
                            });
                        }

                        unprocessedProvinces.Remove(currentProvince);
                        processedProvinces.Add(currentProvince);

                        counties.Last().baronies.Add(new Barony(bi++, currentProvince, currentProvince.Name, currentProvince.Color));

                        Province? neighbor = null;
                        while (true)
                        {
                            neighbor = GetNeighbor(currentProvince, unprocessedProvinces!);

                            if (neighbor is null or { Cells.Count: > 0 })
                            {
                                break;
                            }
                            else if (neighbor.Cells.Count == 0)
                            {
                                // if the province is empty then don't add it.
                                unprocessedProvinces.Remove(neighbor);
                                processedProvinces.Add(neighbor);
                            }
                        }

                        if (neighbor is null)
                        {
                            break;
                        }

                        currentProvince = neighbor;
                    }

                    // If empty then the loop will break anyways.
                    currentProvince = unprocessedProvinces.FirstOrDefault();
                } while (unprocessedProvinces.Count > 0);

                duchies.Add(new Duchy(di++, counties.ToArray(), "Duchy of " + state.name, counties.First().Color, counties.First().CapitalName));
            }
            return duchies;
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }

        Province? GetNeighbor(Province currentProvince, HashSet<Province> unprocessedProvinces)
        {
            return currentProvince.Neighbors.FirstOrDefault(n => unprocessedProvinces.Contains(n));
        }
    }
    public static Empire[] CreateTitles(Map map)
    {
        try
        {
            var duchies = CreateDuchies(map);

            var duchyCultures = new Dictionary<int, List<Duchy>>();
            foreach (var duchy in duchies)
            {
                var primaryDuchyCultureId = duchy.counties
                    .SelectMany(n => n.baronies)
                    .SelectMany(n => n.province.Cells)
                    .Select(n => n.culture)
                    .GroupBy(n => n)
                    .ToDictionary(n => n.Key, n => n.Count())
                    .MaxBy(n => n.Value)
                    .Key;

                if (duchyCultures.TryGetValue(primaryDuchyCultureId, out var dc))
                {
                    dc.Add(duchy);
                }
                else
                {
                    duchyCultures[primaryDuchyCultureId] = [duchy];
                }
            }

            var ki = 1;

            var kingdoms = duchyCultures.Select(n =>
            {
                var cultureId = n.Key;
                var duchies = n.Value;
                return new Kingdom(
                    ki++,
                    duchies.ToArray(),
                    duchies.Count > 1,
                    "Kingdom of " + map.JsonMap.pack.cultures.First(n => n.i == cultureId).name,
                    duchies[0].color,
                    duchies[0].capitalName);
            }).ToArray();

            var kingdomReligions = new Dictionary<int, List<Kingdom>>();
            foreach (var kingdom in kingdoms)
            {
                var primaryDuchyReligionId = kingdom.duchies
                    .SelectMany(n => n.counties)
                    .SelectMany(n => n.baronies)
                    .SelectMany(n => n.province.Cells)
                    .Select(n => n.religion)
                    .GroupBy(n => n)
                    .ToDictionary(n => n.Key, n => n.Count())
                    .MaxBy(n => n.Value)
                    .Key;

                if (kingdomReligions.TryGetValue(primaryDuchyReligionId, out var krs))
                {
                    krs.Add(kingdom);
                }
                else
                {
                    kingdomReligions[primaryDuchyReligionId] = [kingdom];
                }
            }

            var ei = 1;

            var empires = kingdomReligions.Select(n =>
            {
                var religionId = n.Key;
                var kingdoms = n.Value;
                return new Empire(
                    ei++,
                    kingdoms.ToArray(),
                    kingdoms.Count > 1,
                    "Empire of " + map.JsonMap.pack.religions.First(n => n.i == religionId).name,
                    kingdoms[0].color,
                    kingdoms[0].capitalName);
            }).ToArray();

            return empires;
        }
        catch (Exception ex)
        {
            Debugger.Break();
            throw;
        }
    }
    public static async Task WriteLandedTitles(Map map)
    {
        var ci = 1;

        string[] GetBaronies(Barony[] baronies)
        {
            return baronies.Select((n, i) =>
            {
                return $@"                {n.Id} = {{
                    color = {{ {n.color.R} {n.color.G} {n.color.B} }}
                    color2 = {{ 255 255 255 }}
                    province = {map.IdToIndex[n.province.Id]}
                }}";
            }).ToArray();
        }

        string[] GetCounties(County[] counties)
        {
            return counties.Select((n, i) =>
            {
                ci = n.id;
                return $@"            {n.Id} = {{
                color = {{ {n.Color.R} {n.Color.G} {n.Color.B} }}
                color2 = {{ 255 255 255 }}
                definite_form = yes
{string.Join("\n", GetBaronies(n.baronies.ToArray()))}
            }}";
            }).ToArray();
        }

        string[] GetDuchies(Duchy[] duchies)
        {
            return duchies.Select((d, i) => $@"        {d.Id} = {{
            color = {{ {d.color.R} {d.color.G} {d.color.B} }}
            color2 = {{ 255 255 255 }}
            capital = c_{ci}
            definite_form = yes
{string.Join("\n", GetCounties(d.counties))}
        }}").ToArray();
        }

        string[] GetKingdoms(Kingdom[] kingdoms)
        {
            return kingdoms.Select((k, i) => $@"    {k.Id} = {{
        color = {{ {k.color.R} {k.color.G} {k.color.B} }}
        color2 = {{ 255 255 255 }}
        capital = c_{ci}
        definite_form = yes
        {(k.isAllowed ? "" : "allow = { always = no }")}
{string.Join("\n", GetDuchies(k.duchies))}
    }}").ToArray();
        }

        string[] GetEmpires()
        {
            return map.Empires.Select((e, i) => $@"{e.Id} = {{
    color = {{ {e.color.R} {e.color.G} {e.color.B} }}
    color2 = {{ 255 255 255 }}
    capital = c_{ci}
    definite_form = yes
    {(e.isAllowed ? "" : "allow = { always = no }")}
{string.Join("\n", GetKingdoms(e.kingdoms))}
}}").ToArray();
        }

        var file = $@"@correct_culture_primary_score = 100
@better_than_the_alternatives_score = 50
@always_primary_score = 1000
{string.Join("\n", GetEmpires())}
# These titles cut hundreds of errors from logs. 
e_hre = {{ landless = yes }}
e_byzantium = {{ landless = yes }}
e_roman_empire = {{ landless = yes }}";

        var path = Helper.GetPath(Settings.OutputDirectory, "common", "landed_titles", "00_landed_titles.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file);
    }

    public static async Task WriteTitleLocalization(Map map)
    {
        var lines = new List<string>();

        foreach (var e in map.Empires)
        {
            lines.Add($"{e.Id}: \"{e.name}\"");
            foreach (var k in e.kingdoms)
            {
                lines.Add($"{k.Id}: \"{k.name}\"");
                foreach (var d in k.duchies)
                {
                    lines.Add($"{d.Id}: \"{d.name}\"");
                    foreach (var c in d.counties)
                    {
                        lines.Add($"{c.Id}: \"{c.Name}\"");
                        foreach (var b in c.baronies)
                        {
                            lines.Add($"{b.Id}: \"{b.name}\"");
                        }
                    }
                }
            }
        }

        {
            var file = $@"l_english:
 TITLE_NAME:1 ""$NAME|U$""
 TITLE_TIERED_NAME:0 ""$TIER|U$ of $NAME$""
 TITLE_DEFINITIVE_NAME:0 ""the $TIER|U$ of $NAME$""
 TITLE_CLAN_TIERED_NAME:0 ""the $NAME$ $TIER|U$""
 TITLE_CLAN_TIERED_WITH_UNDERLYING_NAME:0 ""the $NAME$ $TIER|U$ #F ($TIER|U$ of $BASE_NAME$) #!""
 TITLE_CLAN_TIERED_WITH_UNDERLYING_NAME_DEFINITE_FORM:0 ""the $NAME$ $TIER|U$ #F ($BASE_NAME$) #!""
 TITLE_TIER_AS_NAME:0 ""$TIER|U$""
 {string.Join("\n ", lines)}";
            var path = Helper.GetPath(Settings.OutputDirectory, "localization", "english", "titles_l_english.yml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await File.WriteAllTextAsync(path, file, new UTF8Encoding(true));
        }
        {
            var file = $@"l_russian:
 TITLE_NAME: ""$NAME|U$""
 TITLE_TIERED_NAME: ""$TIER|U$ $NAME$""
 TITLE_DEFINITIVE_NAME: ""$TIER|U$ $NAME$""
 TITLE_CLAN_TIERED_NAME: ""$TIER|U$ $NAME$ов""
 TITLE_CLAN_TIERED_WITH_UNDERLYING_NAME: ""$TIER|U$ $NAME$ов #F ($TIER|U$ $BASE_NAME$) #!""
 TITLE_CLAN_TIERED_WITH_UNDERLYING_NAME_DEFINITE_FORM: ""$TIER|U$ $NAME$ов #F ($BASE_NAME$) #!""
 TITLE_TIER_AS_NAME: ""$TIER|U$""
 {string.Join("\n ", lines)}";
            var path = Helper.GetPath(Settings.OutputDirectory, "localization", "russian", "titles_l_russian.yml");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            await File.WriteAllTextAsync(path, file, new UTF8Encoding(true));
        }
    }
}
