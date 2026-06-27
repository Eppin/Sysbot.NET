namespace SysBot.Pokemon;

using System;
using System.Threading;
using System.Threading.Tasks;
using Base;
using PKHeX.Core;
using static Base.SwitchButton;
using static Base.SwitchStick;

public class EncounterBotVolcanionLZA(PokeBotState cfg, PokeTradeHub<PA9> hub) : EncounterBotLZA(cfg, hub)
{
    private readonly ushort _volcanion = (ushort)Species.Volcanion;

    protected override async Task EncounterLoop(SAV9ZA sav, CancellationToken token)
    {
        const int maxDurationSeconds = 45 + 100;

        while (!token.IsCancellationRequested)
        {
            var later = DateTime.Now.AddSeconds(maxDurationSeconds);
            await EnableAlwaysCatch(token).ConfigureAwait(false);

            Log("Starting Volcanion encounter sequence");
            await SetStick(LEFT, 0, 30_000, 0_500, token).ConfigureAwait(false);
            await ResetStick(token).ConfigureAwait(false);
            await RepeatClick(A, 100 * 1000, 100, token).ConfigureAwait(false); // 1 minute, 40 seconds

            Log($"Catching Volcanion, wait till [{later}] before we force a game restart", false);

            var result = EncounterResult.Unknown;
            while (result != EncounterResult.VolcanionFound && DateTime.Now <= later)
            {
                await PressAndHold(ZL, 0_250, token).ConfigureAwait(false);
                await Click(ZR, 0_100, token).ConfigureAwait(false);
                await ReleaseHold(ZL, 0_250, token).ConfigureAwait(false);

                result = await LookupSlot(token).ConfigureAwait(false);

                if (result is EncounterResult.InvalidSpecies or EncounterResult.ResultFound)
                    return; // Exit the encounter loop entirely

                if (result is EncounterResult.VolcanionFound)
                {
                    Log("Volcanion found in B1S1, rebooting the game...");
                    break;
                }
            }

            if (DateTime.Now >= later)
                Log("Force restart of the game...");

            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            await Task.Delay(10_000, token).ConfigureAwait(false);
        }
    }

    public override async Task HardStop()
    {
        await ReleaseHold(ZL, 0_500, CancellationToken.None).ConfigureAwait(false);
        await base.HardStop().ConfigureAwait(false);
    }

    private async Task<EncounterResult> LookupSlot(CancellationToken token)
    {
        var (pa9, raw) = await ReadRawBoxPokemon(0, 0, token).ConfigureAwait(false);
        if (pa9.Species > 0 && pa9.Species != _volcanion)
        {
            Log($"Detected species {(Species)pa9.Species}, which shouldn't be possible. Only 'none' or 'Volcanion' are expected");
            return EncounterResult.InvalidSpecies;
        }

        if (pa9.Species == _volcanion && pa9 is { Valid: true, EncryptionConstant: > 0 })
        {
            var (stop, success) = await HandleEncounter(pa9, token, raw, true).ConfigureAwait(false);

            if (success)
                Log("Your Pokémon has been received and placed in B1S1. Auto-save will do the rest!");

            if (stop)
                return EncounterResult.ResultFound;
        }

        return pa9.Species == _volcanion
            ? EncounterResult.VolcanionFound
            : EncounterResult.NextSlot;
    }

    private async Task RepeatClick(SwitchButton button, int duration, int delay, CancellationToken token)
    {
        var endTime = DateTime.Now.AddMilliseconds(duration);

        while (DateTime.Now < endTime)
        {
            await Click(button, delay, token).ConfigureAwait(false);
        }
    }

    private enum EncounterResult
    {
        Unknown,

        InvalidSpecies,
        ResultFound,
        VolcanionFound,
        NextSlot
    }
}
