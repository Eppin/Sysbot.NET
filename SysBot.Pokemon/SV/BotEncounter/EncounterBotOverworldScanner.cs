namespace SysBot.Pokemon;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchStick;

public class EncounterBotOverworldScanner(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotSV(cfg, hub)
{
    private bool _saveKeyInitialized;
    private ulong _baseBlockKeyPointer;

    private readonly List<PK9> _previous = [];

    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
        _baseBlockKeyPointer = await SwitchConnection.PointerAll(Offsets.BlockKeyPointer, token).ConfigureAwait(false);
        _previous.Clear();

        while (!token.IsCancellationRequested)
        {
            switch (Settings.Overworld)
            {
                case EncounterSettingsSV.OverworldMode.Scanner:
                    if (await DoOverworldScanning(token).ConfigureAwait(false))
                        return;

                    await Task.Delay(1000, token);
                    break;

                case EncounterSettingsSV.OverworldMode.ResearchStation:
                    if (await DoResearchStation(token).ConfigureAwait(false))
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
}
