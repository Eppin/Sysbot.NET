namespace SysBot.Pokemon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static PokeDataOffsetsZA;
using static Base.SwitchButton;
using static Base.SwitchStick;

public class EncounterBotOverworldScannerZA(PokeBotState cfg, PokeTradeHub<PA9> hub) : EncounterBotZA(cfg, hub)
{
    private bool _overworldKeyInitialized;
    private bool _shinyEntityKeyInitialized;

    private ulong _speciesCount;
    private ulong _actionCount;

    private readonly List<PA9> _previous = [];
    protected override async Task EncounterLoop(SAV9ZA sav, CancellationToken token)
    {
        _speciesCount = _actionCount = 0;
        _overworldKeyInitialized = _shinyEntityKeyInitialized = false;
        _previous.Clear();

        while (!token.IsCancellationRequested)
        {
            var task = Settings.Overworld.Mode switch
            {
                EncounterSettingsZA.OverworldModeZA.BenchSit => BenchSit(token),
                EncounterSettingsZA.OverworldModeZA.WildZoneEntrance => WildZoneEntrance(token),
                _ => throw new ArgumentOutOfRangeException()
            };
            await task.ConfigureAwait(false);

            await WalkInOverworld(token).ConfigureAwait(false);

            if (await PerformOverworldScan(token).ConfigureAwait(false))
                return;
        }
    }

    private async Task WalkInOverworld(CancellationToken token)
    {
        var walk = Settings.Overworld.WalkDurationMs;
        if (walk > 0)
        {
            Log($"Walking forward for {walk} milliseconds.", false);
            await Run(0, short.MaxValue, walk, token).ConfigureAwait(false);

            Log($"Walking back for {walk} milliseconds.", false);
            await Run(0, short.MinValue, walk, token).ConfigureAwait(false);
        }
    }

    private async Task Run(short x, short y, int walk, CancellationToken token)
    {
        const int defaultDelay = 0_100;

        await SetStick(LEFT, x, y, defaultDelay, token).ConfigureAwait(false);

        // Only press B if the configured walk duration is large enough to fit both stick movement and the B press
        const int clickDelay = defaultDelay * 2;
        if (walk > clickDelay)
            await Click(B, defaultDelay, token).ConfigureAwait(false);

        await Task.Delay(walk, token).ConfigureAwait(false);
        await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false);
    }

    private Task<bool> PerformOverworldScan(CancellationToken token)
    {
        // Determine if slow mode is needed based on shiny search conditions
        // When searching for non-shiny or disabling shiny options, slow mode is required (because only a max. of 10 shinies are stored in a separate block)
        var useSlowMode = Hub.Config.StopConditions.SearchConditions.Any(sc => sc is { IsEnabled: true, ShinyTarget: TargetShinyType.DisableOption or TargetShinyType.NonShiny });

        if (useSlowMode)
        {
            Log("Using the slower, save and full scan, mode", false);
            return DoSlowOverworldScanning(token);
        }

        Log("Using the faster, shiny-only scan, mode", false);
        return DoShinyOverworldScanning(token);
    }

    private async Task<bool> DoSlowOverworldScanning(CancellationToken token)
    {
        //await Bench(token).ConfigureAwait(false);
        await SaveGame(token).ConfigureAwait(false);
        Log("Scanning overworld...");

        await Click(HOME, 0, token).ConfigureAwait(false);
        var results = await GetAllOverworld(token);

        if (await HandleEncounters(results, token)) return true;

        await Click(HOME, 0_500, token).ConfigureAwait(false);
        Log($"Resuming, species found: {_speciesCount}");

        return false;
    }

    private async Task<bool> DoShinyOverworldScanning(CancellationToken token)
    {
        // Overworld spawn check disabled
        var overworld = Settings.Overworld;
        if (overworld.OverworldSpawnCheck == 0)
            return false;

        // Not the time to check yet
        if (_actionCount % (ulong)overworld.OverworldSpawnCheck != 0)
            return false;

        await SaveGame(token).ConfigureAwait(false);
        Log("Scanning overworld...");

        await Click(HOME, 0, token).ConfigureAwait(false);
        var results = await GetShinyOverworld(token);

        if (await HandleEncounters(results, token)) return true;

        if (overworld.StopOnMaxShiniesStored && results.Count >= 10)
        {
            Log("Maximum number of shinies stored in overworld block reached, stopping bot.");
            return true;
        }

        await Click(HOME, 0_500, token).ConfigureAwait(false);
        Log($"Resuming, species found: {_speciesCount}");

        return false;
    }

    private async Task BenchSit(CancellationToken token)
    {
        Log("Moving towards the bench", false);

        await SetStick(LEFT, 0, -30000, 1_000, token).ConfigureAwait(false);
        await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false);

        var later = DateTime.Now.AddSeconds(27);
        Log($"Repeatedly pressing 'A' until [{later}]", false);
        while (DateTime.Now <= later)
            await Click(A, 0_200, token);

        _actionCount++;
    }

    private async Task WildZoneEntrance(CancellationToken token)
    {
        Log("Moving towards the entrance", false);
        await SetStick(LEFT, 0, -30000, 1_000, token).ConfigureAwait(false);
        await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false);

        var later = DateTime.Now.AddSeconds(3);
        Log("Pass entrance", false);
        while (DateTime.Now <= later)
            await Click(A, 0_200, token);

        Log("Moving towards the entrance, again", false);
        await SetStick(LEFT, 0, -30000, 1_000, token).ConfigureAwait(false);
        await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false);

        later = DateTime.Now.AddSeconds(3);
        Log("Pass entrance, again", false);
        while (DateTime.Now <= later)
            await Click(A, 0_200, token);

        _actionCount++;
    }

    private async Task<bool> HandleEncounters(List<PA9> results, CancellationToken token)
    {
        foreach (var current in results)
        {
            if (_previous.Any(p => p.Species == current.Species && p.EncryptionConstant == current.EncryptionConstant && p.PID == current.PID))
                continue;

            var (stop, success) = await HandleEncounter(current, token, minimize: true, skipDump: true).ConfigureAwait(false);
            _speciesCount++;

            if (success)
                Log("Your Pokémon has been found in the overworld!");

            if (stop)
                return true;
        }

        _previous.Clear();
        _previous.AddRange(results);

        return false;
    }

    private async Task<List<PA9>> GetAllOverworld(CancellationToken token)
    {
        var bytes = (await ReadEncryptedBlock(Offsets.KOverworldPointer, KOverworldKey, !_overworldKeyInitialized, token).ConfigureAwait(false)).AsSpan();

        // Only need to initialize once
        _overworldKeyInitialized = true;

        var list = new List<PA9>();

        // Really hacky way to scan for Pokémon in the overworld block
        // just slide over every possible offset and see if a valid PKM is found
        for (var i = 0; i < bytes.Length - FormatSlotSize; i++)
        {
            var entry = bytes.Slice(i, FormatSlotSize);

            if (!EntityDetection.IsPresent(entry)) continue;

            var pa9 = new PA9(entry.ToArray());
            if (!pa9.Valid || pa9.Species <= 0 || pa9.Checksum == 0 || !PersonalTable.ZA.IsSpeciesInGame(pa9.Species)) continue;

            list.Add(pa9);
        }

        return list;
    }

    private async Task<List<PA9>> GetShinyOverworld(CancellationToken token)
    {
        const int size = 0x1F0;

        var bytes = (await ReadEncryptedBlock(Offsets.KStoredShinyEntityPointer, KStoredShinyEntityKey, !_shinyEntityKeyInitialized, token).ConfigureAwait(false)).AsSpan();

        // Only need to initialize once
        _shinyEntityKeyInitialized = true;

        var list = new List<PA9>();
        for (var i = 0; i < 10; i++)
        {
            var ofs = i * size + 8;
            var entry = bytes.Slice(ofs, FormatSlotSize);
            if (EntityDetection.IsPresent(entry))
            {
                var pa9 = new PA9(entry.ToArray());
                list.Add(pa9);
            }
            else
                break;
        }

        return list;
    }
}
