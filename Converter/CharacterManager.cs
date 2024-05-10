using SixLabors.ImageSharp;

namespace Converter;

public static class CharacterManager
{
    public static string OutputDirectory => $"{SettingsManager.Settings.modsDirectory}/{SettingsManager.Settings.modName}";

    // Modifies titles by assigning title holder
    public static async Task<List<Character>> CreateCharacters(Map map)
    {
        // Generate title holders.
        // Capital is the first title in the group.
        // Primary title has all the way to county capital so keep the highest title holder all the way down.

        // Any ducal or kingdom title can be created by a character who controls at least 51% of its De Jure Counties
        // as long as they hold either two titles of lower Rank or one title of equal or higher Rank;
        // empire titles require at least 81% of their De Jure Counties.
        var rnd = new Random(1);
        var characterIndex = 0;
        var characters = new List<Character>();

        foreach (var empire in map.Empires)
        {
            var isEmperor = IsYes(0.1);
            var isEmpire = isEmperor;
            if (isEmperor) empire.holder = GetCharacter();

            foreach (var kingdom in empire.kingdoms)
            {
                var isKing = isEmperor || IsYes(0.2);
                var isKingdom = isKing;
                if (isKing) kingdom.holder = GetCharacter();
                if (!isEmperor && isKing && IsYes(0.81)) kingdom.liege = empire;

                foreach (var duchy in kingdom.duchies)
                {
                    var isDuke = isKing || IsYes(0.3);
                    var isDukedom = isDuke;
                    if (isDuke) duchy.holder = GetCharacter();
                    if (!isKing && isDuke && IsYes(0.51)) duchy.liege = kingdom;

                    foreach (var county in duchy.counties)
                    {
                        // Counties are aumatically generated.
                        // No need to create characters here.

                        if (!isDuke && IsYes(0.51)) county.liege = duchy;

                        isEmperor = false;
                        isKing = false;
                        isDuke = false;
                    }

                    characterIndex++;
                }
            }
        }

        return characters;

        bool IsYes(double probability) => rnd.NextSingle() < probability;
        Character GetCharacter() 
        {
            var c = new Character($"{SettingsManager.Settings.modName}{characterIndex}");
            characters.Add(c);
            return c;
        }
    }
    public static async Task WriteHistoryCharacters(Map map)
    {
        var lines = map.Characters.Select(n => $@"{n.id} = {{
    name = ""Begli"" #anachronistic king of Baguirmi
	dynasty = saodyn001
	religion = west_african_bori_pagan
	culture = sao
    1033.1.1 = {{ birth = yes }}
}}").ToArray();
        var file = string.Join('\n', lines);

        var path = $"{OutputDirectory}/history/characters/all.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file);
    }

    public static async Task WriteHistoryTitles(Map map)
    {
        var lines = new List<string>();

        foreach (var e in map.Empires)
        {
            if (e.holder is not null) lines.Add($"e_{e.id} = {{ 1066.1.1 = {{ holder = {e.holder.id} }} }}");
            foreach (var k in e.kingdoms)
            {
                var kliege = k.liege is not null ? $"liege = e_{k.liege.id}" : null;
                if (k.holder is not null) lines.Add($"k_{k.id} = {{ 1066.1.1 = {{ holder = {k.holder.id} {kliege} }} }}");
                foreach (var d in k.duchies)
                {
                    var dliege = d.liege is not null ? $"liege = k_{d.liege.id}" : null;
                    if (d.holder is not null) lines.Add($"d_{d.id} = {{ 1066.1.1 = {{ holder = {d.holder.id} {dliege} }} }}");

                    foreach (var c in d.counties)
                    {
                        var cliege = c.liege is not null ? $"liege = d_{c.liege.id}" : null;
                        if (cliege is not null) lines.Add($"c_{c.Id} = {{ 1066.1.1 = {{ {cliege} }} }}");
                    }
                }
            }
        }

        var file = string.Join('\n', lines);

        var path = $"{OutputDirectory}/history/titles/all.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        await File.WriteAllTextAsync(path, file);
    }
}
