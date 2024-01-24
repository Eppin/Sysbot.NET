namespace SysBot.Pokemon;

using PKHeX.Core;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System;
using static Base.SwitchButton;

public class EncounterBotResetSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotSV(cfg, hub)
{
    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();

            await SetupBoxState(DumpSetting, token);

            Log("Looking for a Pokémon...");

            PK9? b1s1 = null;

            var later = DateTime.Now.AddMinutes(5);
            Log($"Wait till [{later}] before we force a game restart", false);

            while ((b1s1 == null || (Species)b1s1.Species == Species.None) && DateTime.Now <= later)
            {
                (b1s1, var bytes) = await ReadRawBoxPokemon(0, 0, token).ConfigureAwait(false);

                if (b1s1 is { Valid: true, EncryptionConstant: > 0 } && (Species)b1s1.Species != Species.None)
                {
                    var (stop, success) = await HandleEncounter(b1s1, token, bytes, true).ConfigureAwait(false);

                    if (success)
                        Log("Your Pokémon has been catched and placed in B1S1. Be sure to save your game!");

                    if (stop)
                        return;
                }

                await Click(A, 0_200, token).ConfigureAwait(false);
            }

            if (DateTime.Now >= later)
                Log("Force restart of the game..");

            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            Log($"Single encounter duration: [{sw.Elapsed}]", false);
        }
    }
}
