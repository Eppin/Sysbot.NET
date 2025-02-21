using PKHeX.Core;
using System.Threading;
using FlatbuffersResource;
using System;

namespace SysBot.Pokemon;

public class DenSeedSearchUtil
{
    public static SearchResult? SpecificSeedSearch(RaidSpawnDetail detail, long searchRange, EncounterNest8 raidInfo, NestHoleDistributionEncounter8 raidEventInfo, StopConditionSettings stopConditions, uint flawlessIVs /*out long frames, out ulong seedRes, out ulong threeDay, out string ivSpread*/, CancellationToken token, out string message)
    {
        // By default, set to empty
        message = string.Empty;

        var seed = detail.Seed;
        var rng = new Xoroshiro128Plus(seed);

        for (long i = 0; i < searchRange; i++)
        {
            if (token.IsCancellationRequested)
                return null;

            if (i > 0)
            {
                rng = new Xoroshiro128Plus(seed);
                seed = rng.Next();
                rng = new Xoroshiro128Plus(seed);
            }

            var pk = new PK8();

            var EC = (uint)rng.NextInt();
            var SIDTID = (uint)rng.NextInt();
            var PID = (uint)rng.NextInt();

            pk.EncryptionConstant = EC;
            pk.TID16 = (ushort)(SIDTID >> 16);
            pk.SID16 = (ushort)(SIDTID & 0xffff);
            pk.PID = PID;

            sbyte denAbility;
            uint species;
            uint form;
            sbyte denGender;

            if (detail.IsEvent)
            {
                denAbility = raidEventInfo.Ability;
                species = raidEventInfo.Species;
                form = raidEventInfo.AltForm;
                denGender = raidEventInfo.Gender;
            }
            else
            {
                denAbility = raidInfo.Ability;
                species = raidInfo.Species;
                form = raidInfo.AltForm;
                denGender = raidInfo.Gender;
            }

            rng = GetIVs(rng, flawlessIVs, out var ivs);
            rng = GetAbility(rng, denAbility, out var ability);

            var personalInfo8 = PersonalTable.SWSH.GetFormEntry((ushort)species, (byte)form);
            rng = GetGender(rng, personalInfo8.Gender, denGender, out var gender);

            rng = GetNature(rng, species, (byte)form, out var nature);
            rng = GetHeight(rng, out var height);
            rng = GetWeight(rng, out var weight);

            (ivs[3], ivs[4], ivs[5]) = (ivs[5], ivs[3], ivs[4]);
            pk.SetIVs(ivs);
            pk.SetAbility(ability);
            pk.SetGender((byte)gender);
            pk.Nature = (Nature)nature;
            pk.HeightScalar = height;
            pk.WeightScalar = weight;

            if (!StopConditionSettings.EncounterFound(pk, stopConditions, null, true))
                continue;

            var minus3Days = seed;
            for (var d = 3; d > 0; d--)
                minus3Days -= 0x82A2B175229D6A5B;

            var minus3Advances = i - 3;
            if (minus3Advances < 0)
                continue;

            // Reorder the speed to be last.
            Span<int> pkIVList = stackalloc int[6];
            pk.GetIVs(pkIVList);
            (pkIVList[5], pkIVList[3], pkIVList[4]) = (pkIVList[3], pkIVList[4], pkIVList[5]);
            var pkIVsArr = pkIVList.ToArray();

            message = $"Result found within {i} advances! Seed {seed:X16}, EC {pk.EncryptionConstant:X8}, Shiny: {(pk.IsShiny ? "Yes" : "No")}, Nature: {pk.Nature}, IVs: {string.Join(',', pkIVsArr)}";
            return new SearchResult(i, seed, minus3Advances, minus3Days);
        }

        return null;
    }

    public record SearchResult(long Advances, ulong Seed, long Minus3Advances, ulong Minus3Seed);

    private static Xoroshiro128Plus GetIVs(Xoroshiro128Plus rng, uint flawlessIVs, out int[] ivs)
    {
        // Set IVs that will be 31s
        ivs = [255, 255, 255, 255, 255, 255];
        for (var j = 0; j < flawlessIVs;)
        {
            var index = rng.NextInt(6);
            if (ivs[index] == 255)
            {
                ivs[index] = 31;
                j++;
            }
        }

        // Fill rest of IVs with rand calls
        for (var k = 0; k < ivs.Length; k++)
        {
            if (ivs[k] == 255)
            {
                ivs[k] = (int)rng.NextInt(32);
            }
        }

        return rng;
    }

    private static Xoroshiro128Plus GetGender(Xoroshiro128Plus rng, byte ratioByte, sbyte genderIn, out uint gender)
    {
        var ratio = (GenderRatio)ratioByte;

        gender = genderIn switch
        {
            0 => ratio == GenderRatio.Genderless ? 2 : ratio == GenderRatio.Female ? 1 : ratio == GenderRatio.Male ? 0 : ((rng.NextInt(253) + 1) < (uint)ratio ? (uint)Gender.Female : (uint)Gender.Male),
            1 => 0,
            2 => 1,
            3 => 2,
            _ => (rng.NextInt(253) + 1) < (uint)ratio ? (uint)Gender.Female : (uint)Gender.Male
        };
        return rng;
    }

    private static Xoroshiro128Plus GetAbility(Xoroshiro128Plus rng, sbyte nestAbility, out sbyte ability)
    {
        ability = nestAbility switch
        {
            4 => (sbyte)rng.NextInt(3),
            3 => (sbyte)rng.NextInt(2),
            _ => nestAbility,
        };

        return rng;
    }

    private static Xoroshiro128Plus GetNature(Xoroshiro128Plus rng, uint species, byte form, out uint nature)
    {
        nature = species switch
        {
            (uint)Species.Toxtricity => (uint)ToxtricityUtil.GetRandomNature(ref rng, form),
            _ => (uint)rng.NextInt(25)
        };

        return rng;
    }

    private static Xoroshiro128Plus GetHeight(Xoroshiro128Plus rng, out byte height)
    {
        height = (byte)(rng.NextInt(129) + rng.NextInt(128));
        return rng;
    }

    private static Xoroshiro128Plus GetWeight(Xoroshiro128Plus rng, out byte weight)
    {
        weight = (byte)(rng.NextInt(129) + rng.NextInt(128));
        return rng;
    }

    private enum GenderRatio
    {
        Male = 0,
        Male88 = 31,
        Male75 = 63,
        Even = 127,
        Female75 = 191,
        Female88 = 225,
        Female = 254,
        Genderless = 255
    }

    private enum Gender
    {
        Male,
        Female,
        Genderless,
        Any,
    }
}
