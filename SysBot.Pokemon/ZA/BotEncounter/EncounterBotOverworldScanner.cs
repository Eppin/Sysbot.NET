namespace SysBot.Pokemon.ZA.BotEncounter;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static PokeDataOffsetsZA;
using static Base.SwitchButton;
using static Base.SwitchStick;

public class EncounterBotOverworldScanner(PokeBotState cfg, PokeTradeHub<PA9> hub) : EncounterBotZA(cfg, hub)
{
    private bool _saveKeyInitialized;

    private readonly List<PA9> _previous = [];
    protected override async Task EncounterLoop(SAV9ZA sav, CancellationToken token)
    {
        _saveKeyInitialized = false;
        _previous.Clear();

        while (!token.IsCancellationRequested)
        {
            switch (Settings.Overworld)
            {
                case EncounterSettingsZA.OverworldModeZA.Bench:
                    if (await DoOverworldScanning(token).ConfigureAwait(false))
                        return;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Only need to initialize once
            _saveKeyInitialized = true;
        }
    }

    private async Task<bool> DoOverworldScanning(CancellationToken token)
    {
        Log("Moving towards the bench", false);

        await SetStick(LEFT, 0, -30000, 1_000, token).ConfigureAwait(false);
        await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false);

        var later = DateTime.Now.AddSeconds(10);
        Log($"Repeatedly pressing 'A' until [{later}]", false);
        while (DateTime.Now <= later)
            await Click(A, 0_200, token);

        Log("Scanning overworld...");

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

    private async Task<List<PA9>> GetOverworld(CancellationToken token)
    {
        var bytes = (await ReadEncryptedBlock(Offsets.KOverworldPointer, OverworldBlockKey, !_saveKeyInitialized, token).ConfigureAwait(false)).AsSpan();

        var list = new List<PA9>();

        // Really hacky way to scan for Pokémon in the overworld block
        // just slide over every possible offset and see if a valid PKM is found
        for (var i = 0; i < bytes.Length - BoxFormatSlotSize; i++)
        {
            var entry = bytes.Slice(i, BoxFormatSlotSize);

            if (!EntityDetection.IsPresent(entry)) continue;

            var pa9 = new PA9(entry.ToArray());
            if (!pa9.Valid || pa9.EncryptionConstant == 0 || pa9.PID == 0 || pa9.Species > (ushort)Species.MAX_COUNT) continue;

            list.Add(pa9);
        }

        return list;
    }
}
