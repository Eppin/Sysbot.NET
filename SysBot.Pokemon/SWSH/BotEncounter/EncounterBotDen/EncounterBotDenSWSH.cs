using System.Diagnostics;
using System.Linq;

namespace SysBot.Pokemon;

using FlatbuffersResource;
using Google.FlatBuffers;
using Microsoft.VisualBasic;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsetsSWSH;

public class EncounterBotDenSWSH(PokeBotState cfg, PokeTradeHub<PK8> hub) : EncounterBotSWSH(cfg, hub)
{
    private const string SwordTable = "SysBot.Pokemon.SWSH.BotEncounter.EncounterBotDen.FlatbuffersResource.SwordData.bin";
    private const string ShieldTable = "SysBot.Pokemon.SWSH.BotEncounter.EncounterBotDen.FlatbuffersResource.ShieldData.bin";

    private static byte[] _denTable = [];

    private readonly DenSettings _denSettings = hub.Config.EncounterSWSH.Den;
    private readonly DenMode _denMode = hub.Config.EncounterSWSH.Den.Mode;

    /*
     *if (ulong.TryParse(seedstr, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out ulong seedH))
       BitConverter.GetBytes(seedH).CopyTo(denBytes, 0x8);
     */

    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        _denTable = ReadResourceBinary(sav.Version);

        while (!token.IsCancellationRequested)
        {
            // Throw a Wishing Piece in the Den
            await ThrowWishingPiece(token);

            // Find the active Den
            var activeDen = await HandleActiveDen(token);
            if (activeDen == null)
            {
                if (_denMode.ThrowWishingPiece)
                    continue; // Reset loop

                return; // Exit loop
            }

            // Find a seed, based on the StopConditions
            var result = await HandleSeed(activeDen.Value.Index, activeDen.Value.Detail, sav, _denSettings.Advances, token).ConfigureAwait(false);
            if (result == null)
                return;

            if (result.Minus3Advances < 0)
                continue;

            // Advance towards wanted 'day'
            var advancingSucceeded = await AdvanceDays(result.Minus3Advances, result.Minus3Seed, sav, token).ConfigureAwait(false);
            if (!advancingSucceeded)
                return;

            // Reset for wanted species
            if (Hub.Config.StopConditions.StopOnSpecies != Species.None ||
                Hub.Config.StopConditions.SearchConditions.Any(sc => sc.StopOnSpecies != Species.None))
            {
                await FindSpecies(result.Seed, sav, token).ConfigureAwait(false);
            }

            return;
        }
    }

    private async Task ThrowWishingPiece(CancellationToken token)
    {
        if (!_denMode.ThrowWishingPiece)
            return;

        await Click(A, 4_500, token).ConfigureAwait(false); // Start dialogue
        await Click(A, 4_500, token).ConfigureAwait(false); // Want to throw in a Wishing Piece?
        await Click(A, 0_500, token).ConfigureAwait(false); // Would you like to save the game?
    }

    private async Task<(int Index, RaidSpawnDetail Detail)?> HandleActiveDen(CancellationToken token)
    {
        var active = await FindActiveDen(token).ConfigureAwait(false) ?? throw new Exception("No active Den found, which should be impossible...");
        if (_denSettings.PurpleBeam && !active.Detail.IsRare)
        {
            var message = $"Active Den ({active.Index + 1}) found, but it's not a purple beam.";

            if (_denMode.ThrowWishingPiece)
            {
                Log($"{message} Resetting the game...");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
            else
            {
                Log($"{message} Stopping...");
            }

            return null;
        }

        Log("Active Den found");

        if (_denMode.ThrowWishingPiece)
            await Click(HOME, 2_000 + Hub.Config.Timings.ExtraTimeReturnHome, token).ConfigureAwait(false);

        return active;
    }

    private async Task<(int Index, RaidSpawnDetail Detail)?> FindActiveDen(CancellationToken token)
    {
        var searchTask = _denSettings.Location switch
        {
            DenLocation.Galar => GetDenData(DenVanillaOffset, RaidSpawnList8.RaidCountLegal_O0, token),
            DenLocation.IsleOfArmor => GetDenData(DenIslandOfArmorOffset, RaidSpawnList8.RaidCountLegal_R1, token),
            DenLocation.CrownTundra => GetDenData(DenCrownTundraOffset, RaidSpawnList8.RaidCountLegal_R2, token),
            _ => throw new ArgumentOutOfRangeException()
        };

        var dens = await searchTask.ConfigureAwait(false);
        return dens.SingleOrDefault(den => den.Detail is { IsWishingPiece: true, IsActive: true });
    }

    private async Task<DenSeedSearchUtil.SearchResult?> HandleSeed(int index, RaidSpawnDetail detail, ITrainerInfo sav, long advances, CancellationToken token)
    {
        var eventBytes = await Connection.ReadBytesAsync(DenEventStartOffset, 0x23D4, token).ConfigureAwait(false);

        var nestEvent8 = GetSpawnEvent(detail, sav.Version, eventBytes, out _);
        var nest8 = GetSpawn(index, detail, sav.Version, out _);

        if (detail.IsEvent)
        {
            var speciesEvent = SpeciesName.GetSpeciesNameGeneration((ushort)nestEvent8.Species, sav.Language, sav.Generation);
            Log($"Den {index + 1}, seed: {detail.Seed:X16}, species: {speciesEvent}, {detail.Stars + 1}*");
        }
        else
        {
            var species = SpeciesName.GetSpeciesNameGeneration((ushort)nest8.Species, sav.Language, sav.Generation);
            Log($"Den {index + 1}, seed: {detail.Seed:X16}, species: {species}, {detail.Stars + 1}*");
        }

        var result = await FindSeed(index, detail, sav, advances, token).ConfigureAwait(false);

        if (result == null)
        {
            var noResult = $"No result found within {advances}.";
            if (_denMode.ThrowWishingPiece)
            {
                Log($"{noResult} Resetting the game...");

                await CloseGame(Hub.Config, token, true).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
                return new(-1, 0, -1, 0);
            }

            Log(noResult);
            return null;
        }

        if (_denMode.ThrowWishingPiece)
        {
            await Click(HOME, 1_000, token).ConfigureAwait(false);

            // Make sure to remove open windows
            for (var i = 0; i < 3; i++)
                await Click(B, 1_000, token).ConfigureAwait(false);

            Log("Return to the game");
        }

        return result;
    }

    private async Task<DenSeedSearchUtil.SearchResult?> FindSeed(int index, RaidSpawnDetail detail, ITrainerInfo sav, long advances, CancellationToken token)
    {
        var eventBytes = await Connection.ReadBytesAsync(DenEventStartOffset, 0x23D4, token).ConfigureAwait(false);

        var nestEvent8 = GetSpawnEvent(detail, sav.Version, eventBytes, out _);
        var nest8 = GetSpawn(index, detail, sav.Version, out _);

        if (detail.IsEvent)
        {
            var speciesEvent = SpeciesName.GetSpeciesNameGeneration((ushort)nestEvent8.Species, sav.Language, sav.Generation);
            Log($"Den {index + 1}, seed: {detail.Seed:X16}, species: {speciesEvent}, {detail.Stars + 1}*");
        }
        else
        {
            var species = SpeciesName.GetSpeciesNameGeneration((ushort)nest8.Species, sav.Language, sav.Generation);
            Log($"Den {index + 1}, seed: {detail.Seed:X16}, species: {species}, {detail.Stars + 1}*");
        }

        var result = DenSeedSearchUtil.SpecificSeedSearch(detail, advances, nest8, nestEvent8, Hub.Config.StopConditions, _denSettings.GuaranteedIVs, token, out var message);

        if (!string.IsNullOrWhiteSpace(message))
            Log(message);

        return result;
    }

    private async Task<bool> AdvanceDays(long advances, ulong targetSeed, ITrainerInfo sav, CancellationToken token)
    {
        await SwitchConnection.DateSet(DateTimeOffset.Now.Date, token).ConfigureAwait(false);
        Log($"Need to skip {advances} days");

        var daysToAdd = 0;

        while (advances > 0)
        {
            var daysToSkip = advances == 1
                ? 1
                : advances / 2;

            Log($"Starting to skip {daysToSkip} days");

            for (var days = 0; days < daysToSkip; days++)
            {
                var dateToSet = DateTimeOffset.Now.Date.AddDays(daysToAdd);

                await SwitchConnection.DateSet(dateToSet, token).ConfigureAwait(false);
                await Task.Delay(_denSettings.SkipDelay, token).ConfigureAwait(false);

                if (dateToSet.Date >= new DateTime(2060, 11, 10))
                {
                    Log($"Current date to set is {dateToSet:d}, resetting it...");
                    daysToAdd = 0;
                }
                else
                {
                    daysToAdd++;
                }
            }

            var activeDen = await FindActiveDen(token).ConfigureAwait(false);
            if (activeDen == null)
            {
                Log("Somehow there is no active Den. Stopping...");
                return false;
            }

            var maxAdvances = advances + 1_000; // Let's be on the safe side
            var result = await FindSeed(activeDen.Value.Index, activeDen.Value.Detail, sav, maxAdvances, token).ConfigureAwait(false);
            if (result == null || result.Minus3Seed != targetSeed)
            {
                Log("Somehow there is no result and/or target seed couldn't be found. Stopping...");
                return false;
            }

            advances = result.Minus3Advances;
            Log($"Actual days to advance is {advances}");
            await SaveGame(token).ConfigureAwait(false);
        }

        return true;
    }

    private async Task FindSpecies(ulong targetSeed, ITrainerInfo sav, CancellationToken token)
    {
        await SwitchConnection.DateSet(DateTimeOffset.Now.Date, token).ConfigureAwait(false);
        await Task.Delay(_denSettings.SkipDelay, token).ConfigureAwait(false);
        Log("Starting to skip 3 days");

        var resultFound = false;
        do
        {
            var activeDen = await FindActiveDen(token).ConfigureAwait(false);
            if (activeDen == null)
            {
                Log("Somehow there is no active Den. Stopping...");
                return;
            }

            var days = 1;
            while (activeDen.HasValue && activeDen.Value.Detail.Seed != targetSeed) // TargetSeed +3!!
            {
                await SwitchConnection.DateSet(DateTimeOffset.Now.Date.AddDays(days), token).ConfigureAwait(false);
                await Task.Delay(_denSettings.SkipDelay, token).ConfigureAwait(false);
                days++;

                activeDen = await FindActiveDen(token).ConfigureAwait(false);
            }

            if (activeDen == null)
            {
                Log("Somehow there is no active Den. Stopping...");
                return;
            }

            var eventBytes = await Connection.ReadBytesAsync(DenEventStartOffset, 0x23D4, token).ConfigureAwait(false);

            var species = activeDen.Value.Detail.IsEvent
                ? GetSpawnEvent(activeDen.Value.Detail, sav.Version, eventBytes, out _).Species
                : GetSpawn(activeDen.Value.Index, activeDen.Value.Detail, sav.Version, out _).Species;

            var speciesName = SpeciesName.GetSpeciesNameGeneration((ushort)species, sav.Language, sav.Generation);

            if (Hub.Config.StopConditions.StopOnSpecies == (Species)species ||
                Hub.Config.StopConditions.SearchConditions.Any(sc => sc.StopOnSpecies == (Species)species))
            {
                Log($"Wanted species ({speciesName}) has been found in the Den");
                resultFound = true;
            }
            else
            {
                Log($"Wanted species not found, but we found {speciesName}. Resetting the game...");

                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }

        } while (!resultFound);
    }

    private async Task<List<(int Index, RaidSpawnDetail Detail)>> GetDenData(uint offset, int denCount, CancellationToken token)
    {
        const int denSize = 0x18;

        var result = new List<(int Index, RaidSpawnDetail Detail)>();
        var bytes = await Connection.ReadBytesAsync(offset, denCount * denSize, token);

        for (var i = 0; i < denCount; i++)
        {
            var den = bytes
                .Skip(i * denSize)
                .Take(denSize)
                .ToArray();

            result.Add((i, new RaidSpawnDetail(den)));
        }

        return result;
    }

    private static byte[] ReadResourceBinary(GameVersion version)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(version == GameVersion.SW ? SwordTable : ShieldTable)!;

        var array = new byte[stream.Length];
        _ = stream.Read(array, 0, array.Length);

        return array;
    }

    public EncounterNest8 GetSpawn(int index, RaidSpawnDetail detail, GameVersion version, out EncounterNest8Table tables)
    {
        var indexOffset = _denSettings.Location switch
        {
            DenLocation.Galar => index,
            DenLocation.IsleOfArmor => index + RaidSpawnList8.RaidCountLegal_O0,
            DenLocation.CrownTundra => index + RaidSpawnList8.RaidCountLegal_O0 + RaidSpawnList8.RaidCountLegal_R1,
            _ => throw new ArgumentOutOfRangeException()
        };

        tables = new();
        var data = new ByteBuffer(_denTable);
        var nestTable = EncounterNest8Archive.GetRootAsEncounterNest8Archive(data);
        for (var i = 0; i < nestTable.TablesLength; i++)
        {
            var table = nestTable.Tables(i);
            if (!table.HasValue)
                return new EncounterNest8();

            var denhash = DenHashes.Hashes[indexOffset, detail.IsRare ? 1 : 0];
            if (table.Value.TableID == denhash && table.Value.GameVersion == (version == GameVersion.SW ? 1 : 2))
            {
                tables = table.Value;
                var entryLength = table.Value.EntriesLength;
                var prob = 1;
                for (var p = 0; p < entryLength; p++)
                {
                    var entry = table.Value.Entries(p);
                    if (!entry.HasValue)
                        return new EncounterNest8();

                    prob += (int)entry.Value.Probabilities(detail.Stars);
                    if (prob > detail.RandRoll)
                        return (EncounterNest8)entry;
                }
            }
        }

        return new EncounterNest8();
    }

    public static NestHoleDistributionEncounter8 GetSpawnEvent(RaidSpawnDetail detail, GameVersion version, byte[] data, out NestHoleDistributionEncounter8Table nestEventTable)
    {
        var bb = new ByteBuffer(data);
        var prob = 1;
        nestEventTable = new();
        var tables = NestHoleDistributionEncounter8Archive.GetRootAsNestHoleDistributionEncounter8Archive(bb);
        for (var i = 0; i < tables.TablesLength; i++)
        {
            var table = tables.Tables(i);
            if (!table.HasValue)
                return new NestHoleDistributionEncounter8();

            if (table.Value.GameVersion == (version == GameVersion.SW ? 1 : 2))
            {
                nestEventTable = table.Value;
                var entryLength = table.Value.EntriesLength;
                for (var p = 0; p < entryLength; p++)
                {
                    var entry = table.Value.Entries(p);
                    if (!entry.HasValue)
                        return new NestHoleDistributionEncounter8();

                    prob += (int)entry.Value.Probabilities(detail.Stars);
                    if (prob > detail.RandRoll)
                        return (NestHoleDistributionEncounter8)entry;
                }
            }
        }
        return new NestHoleDistributionEncounter8();
    }
}
