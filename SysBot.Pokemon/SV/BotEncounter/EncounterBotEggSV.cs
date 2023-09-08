﻿namespace SysBot.Pokemon;

using System;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchButton;

public class EncounterBotEggSV : EncounterBotSV
{
    private readonly IDumper _dumpSetting;

    private const int WaitBetweenCollecting = 1; // Seconds
    private static readonly PK9 Blank = new();

    private const int Box = 0;
    private int Slot;

    public EncounterBotEggSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg, hub)
    {
        _dumpSetting = Hub.Config.Folder;
    }

    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
        await SetupBoxState(_dumpSetting, token).ConfigureAwait(false);

        // Start with initial presses
        await Click(A, 250, token).ConfigureAwait(false);
        await Click(A, 250, token).ConfigureAwait(false);

        PK9? previousPk = null;
        var eggsPerBatch = 0;

        while (!token.IsCancellationRequested)
        {
            // Mash A button
            await Click(A, 150, token).ConfigureAwait(false);

            if (eggsPerBatch >= 10)
            {
                eggsPerBatch = 0;
                for (var i = 0; i < 20; i++)
                {
                    await Click(B, 150, token).ConfigureAwait(false);
                }

                Log($"Waiting [{WaitBetweenCollecting}] second(s) for picnic basket to fill");
                for (var i = 0; i < 10; i++)
                    await Click(B, 100, token).ConfigureAwait(false);

                await Task.Delay(TimeSpan.FromSeconds(WaitBetweenCollecting), token).ConfigureAwait(false);
            }

            // Validate result
            var (pk, bytes) = await ReadRawBoxPokemon(Box, Slot, token).ConfigureAwait(false);

            if (previousPk?.EncryptionConstant != pk.EncryptionConstant && (Species)pk.Species != Species.None)
            {
                var (stop, success) = await HandleEncounter(pk, token, bytes, true).ConfigureAwait(false);

                if (success)
                {
                    Log($"You're egg has been claimed and placed in B{Box + 1}S{Slot + 1}. Be sure to save your game!");
                    Slot += 1;
                }

                if (stop)
                    return;

                Log($"Clearing destination slot (B{Box + 1}S{Slot + 1}) for next Egg.", false);
                await SetBoxPokemon(Blank, Box, Slot, token).ConfigureAwait(false);

                previousPk = pk;
                eggsPerBatch++;
            }
        }
    }
}