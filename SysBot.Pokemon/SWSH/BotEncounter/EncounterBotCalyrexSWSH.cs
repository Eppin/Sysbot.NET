namespace SysBot.Pokemon;

using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static Base.SwitchButton;
using static Base.SwitchStick;
using static PokeDataOffsetsSWSH;

public sealed class EncounterBotCalyrexSWSH(PokeBotState cfg, PokeTradeHub<PK8> hub) : EncounterBotEggSWSH(cfg, hub)
{
    protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Log("Looking for a new king and the horse...");

            await EnableAlwaysCatch(token).ConfigureAwait(false);

            // At the start of each loop, an A press is needed to exit out of a prompt.
            await Click(A, 0_100, token).ConfigureAwait(false);
            await SetStick(LEFT, 0, 30000, 1_000, token).ConfigureAwait(false);

            // Encounters Calyrex and clicks through all the menus.
            while (!await IsInBattle(token).ConfigureAwait(false))
                await Click(A, 0_300, token).ConfigureAwait(false);

            Log("Encounter with king started! Checking details...");
            var pk = await ReadUntilPresent(LegendaryPokemonOffset, 2_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (pk == null)
            {
                Log("Invalid data detected. Restarting loop.");
                continue;
            }

            // Get rid of any stick stuff left over so we can flee properly.
            await ResetStick(token).ConfigureAwait(false);

            // Wait for the entire cutscene.
            await Task.Delay(15_000, token).ConfigureAwait(false);

            while (!await IsOnBattleMenu(token).ConfigureAwait(false))
                await Task.Delay(0_100, token).ConfigureAwait(false);
            await Task.Delay(0_100, token).ConfigureAwait(false);

            var (stop, _) = await HandleEncounter(pk, token).ConfigureAwait(false);
            if (stop)
                return;

            Log("Catching Calyrex...");
            await Catch(token).ConfigureAwait(false);

            var later = DateTime.Now.AddMinutes(1);
            Log($"Exit battle, wait till [{later}] before we force a game restart", false);

            PK8? horse = null;
            while (horse is not { Valid: true, Species: > 0 } && DateTime.Now <= later)
            {
                horse = await ReadPokemon(CalyrexFusionSlotOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
                await Click(A, 0_200, token).ConfigureAwait(false);
            }

            if (DateTime.Now >= later)
                Log("Force restart of the game...");
            else
            {
                Log("Checking horse details...");
                (stop, _) = await HandleEncounter(horse, token).ConfigureAwait(false);
                if (stop)
                    return;

                Log("No match, resetting the game...");
            }

            await CloseGame(Hub.Config, token).ConfigureAwait(false);
            await StartGame(Hub.Config, token).ConfigureAwait(false);
        }
    }

    private async Task Catch(CancellationToken token)
    {
        await Click(X, 750, token).ConfigureAwait(false);
        await Click(A, 7_500, token).ConfigureAwait(false);
    }
}
