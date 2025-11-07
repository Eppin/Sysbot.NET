namespace SysBot.Pokemon;

using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static Base.SwitchButton;
using static Base.SwitchStick;

public class EncounterBotOverworldScannerSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotSV(cfg, hub)
{
    private bool _saveKeyInitialized;
    private ulong _baseBlockKeyPointer;

    private readonly List<PK9> _previous = [];

    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
        _saveKeyInitialized = false;
        _baseBlockKeyPointer = await SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
        _previous.Clear();

        while (!token.IsCancellationRequested)
        {
            switch (Settings.Overworld)
            {
                case EncounterSettingsSV.OverworldModeSV.Scanner:
                    if (await DoOverworldScanning(token).ConfigureAwait(false))
                        return;

                    await Task.Delay(1000, token);
                    break;

                case EncounterSettingsSV.OverworldModeSV.ResearchStation:
                    if (await DoResearchStation(token).ConfigureAwait(false))
                        return;
                    break;

                case EncounterSettingsSV.OverworldModeSV.Outbreak:
                    if (await DoMassOutbreakResetting(token).ConfigureAwait(false))
                        return;
                    break;

                case EncounterSettingsSV.OverworldModeSV.KOCount:
                    if (await DoKOCounting(token).ConfigureAwait(false))
                        return;

                    await Task.Delay(5000, token);
                    break;

                case EncounterSettingsSV.OverworldModeSV.Picnic:
                    if (await DoPicnicResetting(token).ConfigureAwait(false))
                        return;
                    break;

                default:
                    Log("Exiting! Invalid overworld mode...");
                    return;
            }

            // Only need to initialize once
            _saveKeyInitialized = true;
        }
    }

    // Return true on success
    private async Task<bool> DoOverworldScanning(CancellationToken token)
    {
        var results = await GetOverworld(token);

        foreach (var current in results)
        {
            if (_previous.Any(p => p.Species == current.Species && p.EncryptionConstant == current.EncryptionConstant && p.PID == current.PID))
                continue;

            var (stop, success) = await HandleEncounter(current, token, skipDump: true).ConfigureAwait(false);

            if (success)
                Log("Your Pokémon has been found in the overworld!");

            if (stop)
                return true;
        }

        _previous.Clear();
        _previous.AddRange(results);

        return false;
    }

    private async Task<bool> DoResearchStation(CancellationToken token)
    {
        Log("In research station", false);

        await SetStick(LEFT, 0, -30000, 6_000, token).ConfigureAwait(false);
        await SetStick(LEFT, 0, 0, 4_000, token).ConfigureAwait(false);

        Log("In overworld, scanning");
        await SaveGame(token).ConfigureAwait(false);
        var results = await GetOverworld(token);

        foreach (var current in results)
        {
            var (stop, success) = await HandleEncounter(current, token, minimize: true, skipDump: true).ConfigureAwait(false);

            if (success)
                Log("Your Pokémon has been found in the overworld!");

            if (stop)
                return true;
        }

        await SetStick(LEFT, 0, -30000, 2_500, token).ConfigureAwait(false);
        await SetStick(LEFT, 0, 0, 2_500, token).ConfigureAwait(false);

        return false;
    }

    private async Task<List<PK9>> GetOverworld(CancellationToken token)
    {
        const int size = 9_360 / 20; // PkHeX: [0x158+7C][20] = 9360 bytes

        var bytes = await ReadEncryptedBlock(_baseBlockKeyPointer, PokeDataOffsetsSV.OverworldBlockKey, !_saveKeyInitialized, token).ConfigureAwait(false);
        var results = new List<PK9>();

        for (var i = 0; i < 20; i++)
        {
            var pk9 = new PK9(bytes.Skip(size * i).Take(size).ToArray());

            if (pk9.Species == (int)Species.None)
                continue;

            results.Add(pk9);
        }

        return results;
    }

    private Dictionary<string, uint> _massOutbreakBlocks = [];
    private async Task<bool> DoMassOutbreakResetting(CancellationToken token)
    {
        GetMassOutbreakBlocks();

        if (await Scan())
            return true;

        if (await Scan(1))
            return true;

        if (await Scan(2))
            return true;

        // Skip datetime
        await SwitchConnection.DateSet(DateTimeOffset.Now.Date, token).ConfigureAwait(false);
        await SaveGame(token).ConfigureAwait(false);

        return false;

        async Task<bool> Scan(int? dlc = null)
        {
            var middle = dlc == null ? "Main" : $"DLC{dlc}";
            var activeSize = await ReadEncryptedBlockByte(_baseBlockKeyPointer, _massOutbreakBlocks[$"KOutbreak{middle}NumActive"], !_saveKeyInitialized, token).ConfigureAwait(false);

            Log($"Scan first {activeSize} outbreaks in {middle} map");

            for (var i = 1; i <= activeSize; i++)
            {
                var speciesData = await ReadEncryptedBlockUInt32(_baseBlockKeyPointer, _massOutbreakBlocks[$"KOutbreak0{i}{middle}Species"], !_saveKeyInitialized, token).ConfigureAwait(false);
                var species = (Species)SpeciesConverter.GetNational9((ushort)speciesData);

                var form = await ReadEncryptedBlockByte(_baseBlockKeyPointer, _massOutbreakBlocks[$"KOutbreak0{i}{middle}Form"], !_saveKeyInitialized, token).ConfigureAwait(false);

                var searchConditions = Hub.Config.EncounterSV.MassOutbreakSearchConditions;

                if (!searchConditions.Any(s => s.IsEnabled))
                    return true;

                var result = searchConditions
                    .Where(s => s.StopOnSpecies == species && s.Form == form && s.IsEnabled)
                    .ToList();

                if (result.Count != 0)
                {
                    Log($"Result found in {middle}: {string.Join(",", result.Select(r => $"{r.StopOnSpecies}-{r.Form}"))}");
                    return true;
                }
            }

            return false;
        }
    }

    private async Task<bool> DoKOCounting(CancellationToken token)
    {
        GetMassOutbreakBlocks();

        if (await Scan())
            return true;

        if (await Scan(1))
            return true;

        if (await Scan(2))
            return true;

        return false;

        async Task<bool> Scan(int? dlc = null)
        {
            var middle = dlc == null ? "Main" : $"DLC{dlc}";
            var activeSize = await ReadEncryptedBlockByte(_baseBlockKeyPointer, _massOutbreakBlocks[$"KOutbreak{middle}NumActive"], !_saveKeyInitialized, token).ConfigureAwait(false);

            var displayed = false;
            for (var i = 1; i <= activeSize; i++)
            {
                var speciesData = await ReadEncryptedBlockUInt32(_baseBlockKeyPointer, _massOutbreakBlocks[$"KOutbreak0{i}{middle}Species"], !_saveKeyInitialized, token).ConfigureAwait(false);
                var species = (Species)SpeciesConverter.GetNational9((ushort)speciesData);

                var form = await ReadEncryptedBlockByte(_baseBlockKeyPointer, _massOutbreakBlocks[$"KOutbreak0{i}{middle}Form"], !_saveKeyInitialized, token).ConfigureAwait(false);

                var koCount = await ReadEncryptedBlockByte(_baseBlockKeyPointer, _massOutbreakBlocks[$"KOutbreak0{i}{middle}NumKOed"], !_saveKeyInitialized, token).ConfigureAwait(false);

                if (koCount > 0)
                {
                    if (!displayed)
                    {
                        Log($"Scan first {activeSize} outbreaks in {middle} map", false);
                        displayed = true;
                    }

                    Log($"KO count of [{koCount}] for {species}-{form}", false);
                }

                if (koCount >= 60)
                    return true;
            }

            return false;
        }
    }

    private async Task<bool> DoPicnicResetting(CancellationToken token)
    {
        Log("Open Picnic");
        await Click(X, 2_500, token).ConfigureAwait(false);
        await Click(A, 6_500, token).ConfigureAwait(false);

        Log("Close Picnic", false);
        await Click(Y, 2_500, token).ConfigureAwait(false);
        await Click(A, 3_500, token).ConfigureAwait(false);

        await SaveGame(token).ConfigureAwait(false);

        Log("In overworld, scanning", false);
        var results = await GetOverworld(token);

        foreach (var current in results)
        {
            var (stop, success) = await HandleEncounter(current, token, minimize: true, skipDump: true).ConfigureAwait(false);

            if (success)
                Log("Your Pokémon has been found in the overworld!");

            if (stop)
                return true;
        }

        return false;
    }

    private void GetMassOutbreakBlocks()
    {
        if (_massOutbreakBlocks.Count != 0) return;

        var endsWith = new List<string> { "NumActive", "Species", "Form", /*"Found",*/ "NumKOed", "TotalSpawns" };

        _massOutbreakBlocks = typeof(SaveBlockAccessor9SV)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.Name.StartsWith("KOutbreak") && !f.Name.Contains("BC") && endsWith.Any(e => f.Name.EndsWith(e)))
            .Select(f => new KeyValuePair<string, uint>(f.Name, (uint)(f.GetRawConstantValue() ?? throw new InvalidOperationException())))
            .ToDictionary();
    }
}
