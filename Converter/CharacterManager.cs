using SixLabors.ImageSharp;
using System.Text;

namespace Converter;

public static class CharacterManager
{
    //private static string[] GetNames()
    //{
    //    var namesPath = Helper.GetPath(Settings.Instance.ck3Directory, "localization\\english\\names/character_names_l_english.yml");
    //    var lines = File.ReadAllLines(namesPath);
    //    var nameRegex = new Regex(@"\s*(\w+)\s*:");
    //    var names = lines.Select(n => nameRegex.Match(n) is { Groups: [_, { Value: { } name }] } ? name : null).Where(n => n is not null).ToArray();
    //    return names;
    //}

    // Modifies titles by assigning title holder
    // Generates titles only in the de ure capitals
    public static async Task<List<Character>> CreateCharacters(Map map)
    {
        //var names = GetNames();

        // Generate title holders.
        // Capital is the first title in the group.
        // Primary title has all the way to county capital so keep the highest title holder all the way down.

        // Any ducal or kingdom title can be created by a character who controls at least 51% of its De Jure Counties
        // as long as they hold either two titles of lower Rank or one title of equal or higher Rank;
        // empire titles require at least 81% of their De Jure Counties.
        var rnd = new Random(1);
        var characters = new List<Character>();

        // The average starting limit is 4 but we assume
        const double HoldingLimit = 2;
        int i = 0;

        foreach (var empire in map.Output.Empires)
        {
            var isEmperor = IsYes(0.1);
            var isEmpire = isEmperor;
            if (isEmperor) empire.holder = GetNewCharacter(empire);

            int empireHoldingCount = 0;
            double empireHoldingLimit = isEmperor ? GetCharacter().stewardshipSkill / 6 + HoldingLimit : 0;
            double GetEmpireEncoragement() => empireHoldingCount > 0 ? (empireHoldingCount / empireHoldingLimit) : 0;

            foreach (var kingdom in empire.kingdoms)
            {
                var isKing = isEmperor || IsYes(0.3);
                var isKingdom = isKing;
                if (isKing) kingdom.holder = GetNewCharacter(kingdom);
                else GetCharacter();
                if (!isEmperor && isEmpire)
                {
                    if (isKing && IsYes(0.81)) kingdom.liege = empire;
                }

                int kingdomHoldingCount = 0;
                double kingdomHoldingLimit = isKing ? GetCharacter().stewardshipSkill / 6 + HoldingLimit : 0;
                double GetKingdomEncoragement() => kingdomHoldingCount > 0 ? (kingdomHoldingCount / kingdomHoldingLimit) : 0;

                foreach (var duchy in kingdom.duchies)
                {
                    var isDuke = isKing || IsYes(0.6);
                    var isDukedom = isDuke;
                    if (isDuke) duchy.holder = GetNewCharacter(duchy);
                    else GetCharacter();
                    if (!isKing && isKingdom)
                    {
                        if (isDuke && IsYes(0.51)) duchy.liege = kingdom;
                        else if (isEmpire && IsYes(0.81)) duchy.liege = empire;
                    }

                    int duchyHoldingCount = 0;
                    double duchyHoldingLimit = isDuke ? GetCharacter().stewardshipSkill / 6 + HoldingLimit : 0;
                    double GetDuchyEncoragement() => duchyHoldingCount > 0 ? (duchyHoldingCount / duchyHoldingLimit) : 0;

                    var countyHoldingCount = 0;
                    double countyHoldingLimit = HoldingLimit;
                    double GetCountyEncoragement() => countyHoldingCount > 0 ? (countyHoldingCount / countyHoldingLimit) : 0;

                    foreach (var county in duchy.counties)
                    {
                        if (IsYes(GetCountyEncoragement()))
                        {
                            county.holder = GetNewCharacter(county);
                        }
                        else
                        {
                            GetCharacter();
                            countyHoldingCount++;
                        }

                        // Only assign liege 1 time
                        if (countyHoldingCount == 1)
                        {
                            if (!isDuke && isDukedom)
                            {
                                if (IsYes(0.51 - GetDuchyEncoragement()))
                                {
                                    county.liege = duchy;
                                    duchyHoldingCount++;
                                }
                                else if (isKingdom && IsYes(0.51 - GetKingdomEncoragement()))
                                {
                                    county.liege = kingdom;
                                    kingdomHoldingCount++;
                                }
                                else if (isEmpire && IsYes(0.81 - GetEmpireEncoragement()))
                                {
                                    county.liege = empire;
                                    empireHoldingCount++;
                                }
                            }
                            else if (!isKing && isKingdom)
                            {
                                if (isKingdom && IsYes(0.51 - GetKingdomEncoragement()))
                                {
                                    county.liege = kingdom;
                                    kingdomHoldingCount++;
                                }
                                else if (isEmpire && IsYes(0.81 - GetEmpireEncoragement()))
                                {
                                    county.liege = empire;
                                    empireHoldingCount++;
                                }
                            }
                            else if (!isEmperor && isEmpire)
                            {
                                if (isEmpire && IsYes(0.81 - GetEmpireEncoragement()))
                                {
                                    county.liege = empire;
                                    empireHoldingCount++;
                                }
                            }
                        }

                        isEmperor = false;
                        isKing = false;
                        isDuke = false;
                        i++;
                    }
                }
            }
        }

        return characters;

        bool IsYes(double probability) => rnd.NextSingle() < probability;
        Character GetCharacter()
        {
            return characters.Last();
        }
        Character GetNewCharacter(ICultureReligionHolder crh)
        {
            var age = rnd.Next(4, 70);
            var stewardship = rnd.Next(age / 2, age - 2) / 2;
            var dynastyName = map.Output.NameBase.names[rnd.Next(map.Output.NameBase.names.Length)];

            //var name = names[rnd.Next(names.Length)];

            var c = new Character($"{Settings.Instance.ModName}{characters.Count}", dynastyName, crh.Culture, crh.Religion, age, stewardship, dynastyName);
            characters.Add(c);
            return c;
        }
    }

    public static async Task<List<Character>> CreateCharactersCountOnly(Map map)
    {
        // Generate title holders.
        // Capital is the first title in the group.
        // Primary title has all the way to county capital so keep the highest title holder all the way down.

        var rnd = new Random(1);
        var characters = new List<Character>();

        var counties = map.Output.Empires.SelectMany(n => n.kingdoms).SelectMany(n => n.duchies).SelectMany(n => n.counties).ToArray();
        foreach (var county in counties)
        {
            if (IsYes(0.8))
            {
                county.holder = GetNewCharacter(county);
            }
            else
            {
                GetCharacter();
            }
        }
        
        return characters;

        bool IsYes(double probability) => rnd.NextSingle() < probability;
        Character GetCharacter()
        {
            return characters.Last();
        }
        Character GetNewCharacter(ICultureReligionHolder crh)
        {
            var age = rnd.Next(4, 70);
            var stewardship = rnd.Next(age / 2, age - 2) / 2;
            var dynastyName = map.Output.NameBase.names[rnd.Next(map.Output.NameBase.names.Length)];

            //var name = names[rnd.Next(names.Length)];

            var c = new Character($"{Settings.Instance.ModName}{characters.Count}", dynastyName, crh.Culture, crh.Religion, age, stewardship, dynastyName);
            characters.Add(c);
            return c;
        }
    }


    public static async Task WriteHistoryCharacters(Map map)
    {
        var rnd = new Random(1);
        var lines = map.Output.Characters.Select(n => $@"{n.id} = {{
    name = ""{n.name.name}""
    dynasty = ""{n.dynastyName.id}""
    religion = ""{n.religion}""
    culture = ""{n.culture}""
    stewardship = {n.stewardshipSkill}
    {1066 - n.age}.1.1 = {{ birth = ""{1066 - n.age}.1.1"" }}
}}").ToArray();
        var file = string.Join('\n', lines);

        // Delete existing characters.
        Directory.EnumerateFiles(Helper.GetPath(Settings.OutputDirectory, "history", "characters")).ToList().ForEach(File.Delete);

        var path = Helper.GetPath(Settings.OutputDirectory, "history", "characters", "all.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file);
    }

    public static async Task WriteHistoryTitles(Map map)
    {
        var lines = new List<string>();

        foreach (var e in map.Output.Empires)
        {
            if (e.holder is not null) lines.Add($"{e.Id} = {{ 1066.1.1 = {{ holder = {e.holder.id} }} }}");
            foreach (var k in e.kingdoms)
            {
                var kliege = k.liege is not null ? $"liege = \"{k.liege.Id}\"" : null;
                if (k.holder is not null) lines.Add($"{k.Id} = {{ 1066.1.1 = {{ holder = {k.holder.id} {kliege} }} }}");
                foreach (var d in k.duchies)
                {
                    var dliege = d.liege is not null ? $"liege = \"{d.liege.Id}\"" : null;
                    if (d.holder is not null) lines.Add($"{d.Id} = {{ 1066.1.1 = {{ holder = {d.holder.id} {dliege} }} }}");
                    foreach (var c in d.counties)
                    {
                        var cliege = c.liege is not null ? $"liege = \"{c.liege.Id}\"" : null;
                        if (c.holder is not null) lines.Add($"{c.Id} = {{ 1066.1.1 = {{ holder = {c.holder?.id} {cliege} }} }}");
                    }
                }
            }
        }

        var file = string.Join('\n', lines);

        // Delete existing titles.
        Directory.EnumerateFiles(Helper.GetPath(Settings.OutputDirectory, "history", "titles")).ToList().ForEach(File.Delete);

        var path = Helper.GetPath(Settings.OutputDirectory, "history", "titles", "all.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file);
    }
    public static async Task WriteDynasties(Map map)
    {
        var lines = map.Output.Characters.DistinctBy(n => n.dynastyName).Select(n => $@"{n.dynastyName.id} = {{
    name = ""dynn_{n.dynastyName.id}""
    culture = ""{n.culture}""
}}");
        var file = string.Join('\n', lines);

        // Delete existing dynasties.
        Directory.EnumerateFiles(Helper.GetPath(Settings.OutputDirectory, "common", "dynasties")).ToList().ForEach(File.Delete);

        var path = Helper.GetPath(Settings.OutputDirectory, "common", "dynasties", "all.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file, new UTF8Encoding(true));
    }
    public static async Task WriteDynastyLocalization(Map map)
    {
        var lines = map.Output.Characters.DistinctBy(n => n.dynastyName).Select(n => $"dynn_{n.dynastyName.id}: \"{n.dynastyName.name}\"");
        var content = string.Join("\n ", lines);

        await Helper.WriteLocalizationFile(map, "dynasties", "dynasty_names_l_", content, "FOUNDER_BASED_NAME_POSTFIX");
    }
}
