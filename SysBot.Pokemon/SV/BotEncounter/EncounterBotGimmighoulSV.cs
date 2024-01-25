namespace SysBot.Pokemon;

using System.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchButton;

public class EncounterBotGimmighoulSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : EncounterBotSV(cfg, hub)
{
    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();

            await SetupBoxState(DumpSetting, token);
            await EnableAlwaysCatch(token).ConfigureAwait(false);

            Log("Start battle with Gimmighoul");
            var later = DateTime.Now.AddSeconds(2);
            Log($"Press A till [{later}]", false);
            while (DateTime.Now <= later)
                await Click(A, 200, token);

            later = DateTime.Now.AddSeconds(5);
            Log($"Press B till [{later}]", false);
            while (DateTime.Now <= later)
                await Click(B, 200, token);

            Log("Catch using default ball");
            await Click(X, 750, token);
            await Click(A, 7_500, token);

            later = DateTime.Now.AddMinutes(1);
            Log($"Exit battle, wait till [{later}] before we force a game restart", false);
            PK9? b1s1 = null;

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

                await Click(B, 200, token).ConfigureAwait(false);
            }

            if (DateTime.Now >= later)
                Log("Force restart of the game..");

            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            Log($"Single encounter duration: [{sw.Elapsed}]", false);
        }
    }
}
