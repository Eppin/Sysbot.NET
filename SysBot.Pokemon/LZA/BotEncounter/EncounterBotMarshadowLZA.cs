namespace SysBot.Pokemon;

using System;
using System.Threading;
using System.Threading.Tasks;
using Base;
using PKHeX.Core;
using static Base.SwitchButton;
using static Base.SwitchStick;

public class EncounterBotMarshadowLZA(PokeBotState cfg, PokeTradeHub<PA9> hub) : EncounterBotLZA(cfg, hub)
{
    private const ushort MarshadowSpecies = (ushort)Species.Marshadow;

    private const int EncounterTimeoutSeconds = 100;
    private const int MashDurationMs = 60_000;
    private const int MashDelayMs = 100;
    private const int ReopenDelayMs = 10_000;

    protected override async Task EncounterLoop(SAV9ZA sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var deadline = DateTime.Now.AddSeconds(EncounterTimeoutSeconds);

            await EnableAlwaysCatch(token).ConfigureAwait(false);

            Log("Starting Marshadow encounter sequence");
            await SetStick(LEFT, 0, 30_000, 0_500, token).ConfigureAwait(false);
            await ResetStick(token).ConfigureAwait(false);
            await RepeatClick(A, MashDurationMs, MashDelayMs, token).ConfigureAwait(false);

            Log($"Catching Marshadow, wait till [{deadline}] before we force a game restart", false);

            await SetStick(LEFT, 0, 30_000, 1_000, token).ConfigureAwait(false);
            await ResetStick(token).ConfigureAwait(false);

            var result = await TryEncounterUntilDeadline(deadline, token).ConfigureAwait(false);

            if (result is EncounterResult.InvalidSpecies or EncounterResult.ResultFound)
                return;

            if (result == EncounterResult.Timeout)
                Log("Force restart of the game...");

            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            await Task.Delay(ReopenDelayMs, token).ConfigureAwait(false);
        }
    }

    public override async Task HardStop()
    {
        await ReleaseHold(ZL, 0_500, CancellationToken.None).ConfigureAwait(false);
        await base.HardStop().ConfigureAwait(false);
    }

    private async Task<EncounterResult> TryEncounterUntilDeadline(DateTime deadline, CancellationToken token)
    {
        while (DateTime.Now <= deadline && !token.IsCancellationRequested)
        {
            await RunEncounterInput(token).ConfigureAwait(false);

            var result = await LookupSlot(token).ConfigureAwait(false);
            if (result is EncounterResult.InvalidSpecies or EncounterResult.ResultFound)
                return result;

            if (result == EncounterResult.MarshadowFound)
            {
                Log("Marshadow found in B1S1, rebooting the game...");
                return result;
            }
        }

        return EncounterResult.Timeout;
    }

    private async Task RunEncounterInput(CancellationToken token)
    {
        await PressAndHold(ZL, 0_250, token).ConfigureAwait(false);
        await Click(ZR, 0_100, token).ConfigureAwait(false);
        await ReleaseHold(ZL, 0_250, token).ConfigureAwait(false);
    }

    private async Task<EncounterResult> LookupSlot(CancellationToken token)
    {
        var (pa9, raw) = await ReadRawBoxPokemon(0, 0, token).ConfigureAwait(false);

        if (pa9.Species > 0 && pa9.Species != MarshadowSpecies)
        {
            Log($"Detected species {(Species)pa9.Species}, which shouldn't be possible. Only 'none' or 'Marshadow' are expected");
            return EncounterResult.InvalidSpecies;
        }

        if (pa9.Species == MarshadowSpecies && pa9 is { Valid: true, EncryptionConstant: > 0 })
        {
            var (stop, success) = await HandleEncounter(pa9, token, raw, true).ConfigureAwait(false);

            if (success)
                Log("Your Pokemon has been received and placed in B1S1. Auto-save will do the rest!");

            if (stop)
                return EncounterResult.ResultFound;
        }

        return pa9.Species == MarshadowSpecies
            ? EncounterResult.MarshadowFound
            : EncounterResult.NextSlot;
    }

    private async Task RepeatClick(SwitchButton button, int durationMs, int delayMs, CancellationToken token)
    {
        var endUtc = DateTime.UtcNow.AddMilliseconds(durationMs);

        while (DateTime.UtcNow < endUtc && !token.IsCancellationRequested)
            await Click(button, delayMs, token).ConfigureAwait(false);
    }

    private enum EncounterResult
    {
        InvalidSpecies,
        ResultFound,
        MarshadowFound,
        NextSlot,
        Timeout
    }
}
