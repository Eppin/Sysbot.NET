namespace SysBot.Pokemon;

using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

public class OutputExtensions<T> where T : PKM, new()
{
    private static readonly object LockObj = new();

    public static void EncounterLogs(PKM pk, string filepath = "")
    {
        if (string.IsNullOrEmpty(filepath))
            filepath = "EncounterLogPretty.txt";

        if (!File.Exists(filepath))
        {
            const string blank = "Totals: 0 Pokémon, 0 Eggs, 0 ★, 0 ■, 0 🎀\n_________________________________________________\n";
            File.WriteAllText(filepath, blank);
        }

        lock (LockObj)
        {
            var mark = pk switch
            {
                PK8 => pk is PK8 pk8 && pk8.HasEncounterMark(),
                PK9 => pk is PK9 pk9 && pk9.HasEncounterMark(),
                _ => false
            };

            var content = File.ReadAllText(filepath).Split('\n').ToList();
            var splitTotal = content[0].Split(',');
            content.RemoveRange(0, 3);

            var pokeTotal = int.Parse(splitTotal[0].Split(' ')[1]) + 1;
            var eggTotal = int.Parse(splitTotal[1].Split(' ')[1]) + (pk.IsEgg ? 1 : 0);
            var starTotal = int.Parse(splitTotal[2].Split(' ')[1]) + (pk is { IsShiny: true, ShinyXor: > 0 } ? 1 : 0);
            var squareTotal = int.Parse(splitTotal[3].Split(' ')[1]) + (pk is { IsShiny: true, ShinyXor: 0 } ? 1 : 0);
            var markTotal = int.Parse(splitTotal[4].Split(' ')[1]) + (mark ? 1 : 0);

            var form = FormOutput(pk.Species, pk.Form, out _);
            var speciesName = $"{SpeciesName.GetSpeciesNameGeneration(pk.Species, pk.Language, 8)}{form}".Replace(" ", "");
            var index = content.FindIndex(x => x.Split(':')[0].Equals(speciesName));

            if (index == -1)
                content.Add($"{speciesName}: 1, {(pk.IsShiny && pk.ShinyXor > 0 ? 1 : 0)}★, {(pk.IsShiny && pk.ShinyXor == 0 ? 1 : 0)}■, {(mark ? 1 : 0)}🎀, {GetPercent(pokeTotal, 1)}%");

            var length = index == -1 ? 1 : 0;
            for (var i = 0; i < content.Count - length; i++)
            {
                var sanitized = GetSanitizedEncounterLineArray(content[i]);
                if (i == index)
                {
                    var speciesTotal = int.Parse(sanitized[1]) + 1;
                    var stTotal = int.Parse(sanitized[2]) + (pk.IsShiny && pk.ShinyXor > 0 ? 1 : 0);
                    var sqTotal = int.Parse(sanitized[3]) + (pk.IsShiny && pk.ShinyXor == 0 ? 1 : 0);
                    var mTotal = int.Parse(sanitized[4]) + (mark ? 1 : 0);
                    content[i] = $"{speciesName}: {speciesTotal}, {stTotal}★, {sqTotal}■, {mTotal}🎀, {GetPercent(pokeTotal, speciesTotal)}%";
                }
                else
                {
                    content[i] = $"{sanitized[0]} {sanitized[1]}, {sanitized[2]}★, {sanitized[3]}■, {sanitized[4]}🎀, {GetPercent(pokeTotal, int.Parse(sanitized[1]))}%";
                }
            }

            content.Sort();
            var totalsString =
                $"Totals: {pokeTotal} Pokémon, " +
                $"{eggTotal} Eggs ({GetPercent(pokeTotal, eggTotal)}%), " +
                $"{starTotal} ★ ({GetPercent(pokeTotal, starTotal)}%), " +
                $"{squareTotal} ■ ({GetPercent(pokeTotal, squareTotal)}%), " +
                $"{markTotal} 🎀 ({GetPercent(pokeTotal, markTotal)}%)" +
                "\n_________________________________________________\n";
            content.Insert(0, totalsString);
            File.WriteAllText(filepath, string.Join("\n", content));
        }
    }

    public static void EncounterScaleLogs(PK9 pk, string filepath = "")
    {
        if (filepath == "")
            filepath = "EncounterScaleLogPretty.txt";

        if (!File.Exists(filepath))
        {
            const string blank = "Totals: 0 Pokémon, 0 Mini, 0 Jumbo, 0 Miscellaneous\n_________________________________________________\n";
            File.WriteAllText(filepath, blank);
        }

        lock (LockObj)
        {
            var content = File.ReadAllText(filepath).Split('\n').ToList();
            var splitTotal = content[0].Split(',');
            content.RemoveRange(0, 3);

            var isMini = pk.Scale == 0;
            var isJumbo = pk.Scale == 255;
            var isMisc = pk.Scale is > 0 and < 255;
            var pokeTotal = int.Parse(splitTotal[0].Split(' ')[1]) + 1;
            var miniTotal = int.Parse(splitTotal[1].Split(' ')[1]) + (isMini ? 1 : 0);
            var jumboTotal = int.Parse(splitTotal[2].Split(' ')[1]) + (isJumbo ? 1 : 0);
            var otherTotal = int.Parse(splitTotal[3].Split(' ')[1]) + (isMisc ? 1 : 0);

            var form = FormOutput(pk.Species, pk.Form, out _);
            var speciesName = $"{SpeciesName.GetSpeciesNameGeneration(pk.Species, pk.Language, 9)}{form}".Replace(" ", "");
            var index = content.FindIndex(x => x.Split(':')[0].Equals(speciesName));

            if (index == -1)
                content.Add($"{speciesName}: 1, {(isMini ? 1 : 0)} Mini, {(isJumbo ? 1 : 0)} Jumbo, {(isMisc ? 1 : 0)} Miscellaneous");

            var length = index == -1 ? 1 : 0;
            for (var i = 0; i < content.Count - length; i++)
            {
                var sanitized = GetSanitizedEncounterScaleArray(content[i]);
                if (i == index)
                {
                    var speciesTotal = int.Parse(sanitized[1]) + 1;
                    var miTotal = int.Parse(sanitized[2]) + (isMini ? 1 : 0);
                    var juTotal = int.Parse(sanitized[3]) + (isJumbo ? 1 : 0);
                    var otTotal = int.Parse(sanitized[4]) + (isMisc ? 1 : 0);
                    content[i] = $"{speciesName}: {speciesTotal}, {miTotal} Mini, {juTotal} Jumbo, {otTotal} Miscellaneous";
                }
                else content[i] = $"{sanitized[0]} {sanitized[1]}, {sanitized[2]} Mini, {sanitized[3]} Jumbo, {sanitized[4]} Miscellaneous";
            }

            content.Sort();
            var totalsString =
                $"Totals: {pokeTotal} Pokémon, " +
                $"{miniTotal} Mini ({GetPercent(pokeTotal, miniTotal)}%), " +
                $"{jumboTotal} Jumbo ({GetPercent(pokeTotal, jumboTotal)}%), " +
                $"{otherTotal} Miscellaneous ({GetPercent(pokeTotal, otherTotal)}%)" +
                "\n_________________________________________________\n";
            content.Insert(0, totalsString);
            File.WriteAllText(filepath, string.Join("\n", content));
        }
    }

    private static string GetPercent(int total, int subtotal) => (100.0 * ((double)subtotal / total)).ToString("N2", NumberFormatInfo.InvariantInfo);

    private static string[] GetSanitizedEncounterScaleArray(string content)
    {
        var replace = new Dictionary<string, string> { { ",", "" }, { " Mini", "" }, { " Jumbo", "" }, { " Miscellaneous", "" }, { "%", "" } };
        return replace.Aggregate(content, (old, cleaned) => old.Replace(cleaned.Key, cleaned.Value)).Split(' ');
    }

    private static string[] GetSanitizedEncounterLineArray(string content)
    {
        var replace = new Dictionary<string, string> { { ",", "" }, { "★", "" }, { "■", "" }, { "🎀", "" }, { "%", "" } };
        return replace.Aggregate(content, (old, cleaned) => old.Replace(cleaned.Key, cleaned.Value)).Split(' ');
    }

    public static string PokeImage(T pkm, bool canGmax, bool fullSize)
    {
        var md = false;
        var fd = false;
        var baseLink = fullSize
            ? "https://raw.githubusercontent.com/zyro670/HomeImages/master/512x512/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_')
            : "https://raw.githubusercontent.com/zyro670/HomeImages/master/128x128/poke_capture_0001_000_mf_n_00000000_f_n.png".Split('_');

        if (Enum.IsDefined(typeof(GenderDependent), pkm.Species) && !canGmax && pkm.Form is 0)
        {
            if (pkm.Gender == 0 && pkm.Species != (int)Species.Torchic)
                md = true;
            else fd = true;
        }

        var form = pkm.Species switch
        {
            (int)Species.Sinistea or (int)Species.Polteageist or (int)Species.Rockruff or (int)Species.Mothim => 0,
            (int)Species.Alcremie when pkm.IsShiny || canGmax => 0,
            _ => pkm.Form,

        };

        if (pkm.Species is (ushort)Species.Sneasel)
        {
            if (pkm.Gender is 0)
                md = true;
            else fd = true;
        }

        if (pkm.Species is (ushort)Species.Basculegion)
        {
            if (pkm.Gender is 0)
            {
                md = true;
                pkm.Form = 0;
            }
            else
                pkm.Form = 1;

            var s = pkm.IsShiny ? "r" : "n";
            var g = md && pkm.Gender is not 1 ? "md" : "fd";
            return $"https://raw.githubusercontent.com/zyro670/HomeImages/master/128x128/poke_capture_0" + $"{pkm.Species}" + "_00" + $"{pkm.Form}" + "_" + $"{g}" + "_n_00000000_f_" + $"{s}" + ".png";
        }

        baseLink[2] = pkm.Species < 10 ? $"000{pkm.Species}" : pkm.Species < 100 && pkm.Species > 9 ? $"00{pkm.Species}" : pkm.Species >= 1000 ? $"{pkm.Species}" : $"0{pkm.Species}";
        baseLink[3] = pkm.Form < 10 ? $"00{form}" : $"0{form}";
        baseLink[4] = pkm.PersonalInfo.OnlyFemale ? "fo" : pkm.PersonalInfo.OnlyMale ? "mo" : pkm.PersonalInfo.Genderless ? "uk" : fd ? "fd" : md ? "md" : "mf";
        baseLink[5] = canGmax ? "g" : "n";
        baseLink[6] = "0000000" + (pkm.Species == (int)Species.Alcremie && !canGmax ? pkm.Data[0xE4] : 0);
        baseLink[8] = pkm.IsShiny ? "r.png" : "n.png";
        return string.Join("_", baseLink);
    }

    public static string FormOutput(ushort species, byte form, out string[] formString)
    {
        var strings = GameInfo.GetStrings("en");
        formString = FormConverter.GetFormList(species, strings.Types, strings.forms, GameInfo.GenderSymbolASCII, typeof(T) == typeof(PK9) ? EntityContext.Gen9 : EntityContext.Gen4);
        if (formString.Length is 0)
            return string.Empty;

        formString[0] = "";
        if (form >= formString.Length)
            form = (byte)(formString.Length - 1);
        return formString[form].Contains('-') ? formString[form] : formString[form] == "" ? "" : $"-{formString[form]}";
    }
}
