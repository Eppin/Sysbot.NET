namespace SysBot.Pokemon;

using PKHeX.Core;
using SysBot.Base;
using System;
using System.Threading;
using System.Threading.Tasks;
using static Base.SwitchButton;
using static Base.SwitchStick;

public class EncounterBotDiancieLZA(PokeBotState cfg, PokeTradeHub<PA9> hub) : EncounterBotLZA(cfg, hub)
{
    private readonly ushort _diancie = (ushort)Species.Diancie;
    private readonly ushort _carbink = (ushort)Species.Carbink;

    protected override async Task EncounterLoop(SAV9ZA sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await EnableAlwaysCatch(token).ConfigureAwait(false);

            Log("Starting Diancie encounter sequence");
            await SetStick(LEFT, 0, 30_000, 0_500, token).ConfigureAwait(false);
            await ResetStick(token).ConfigureAwait(false);
            await RepeatClick(A, 20_000, 100, token).ConfigureAwait(false);

            Log("Catching Diancie");

            var result = EncounterResult.Unknown;
            while (result != EncounterResult.DiancieFound)
            {
                await PressAndHold(ZL, 0_250, token).ConfigureAwait(false);
                await Click(ZR, 0_100, token).ConfigureAwait(false);
                await ReleaseHold(ZL, 0_250, token).ConfigureAwait(false);

                for (int slot = 0; slot < 3; slot++)
                {
                    result = await LookupSlot(slot, token).ConfigureAwait(false);

                    if (result is EncounterResult.InvalidSpecies or EncounterResult.ResultFound)
                    {
                        return; // Exit the encounter loop entirely
                    }

                    if (result is EncounterResult.DiancieFound)
                    {
                        Log($"Diancie found in B1S{slot + 1}, rebooting the game...");
                        break;
                    }
                }
            }

            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            await Task.Delay(10_000, token).ConfigureAwait(false);
        }
    }

    public override async Task HardStop()
    {
        await ReleaseHold(ZL, 0_500, CancellationToken.None).ConfigureAwait(false);
        await base.HardStop().ConfigureAwait(false);
    }

    private async Task<EncounterResult> LookupSlot(int slot, CancellationToken token)
    {
        (var pa9, var raw) = await ReadRawBoxPokemon(0, slot, token).ConfigureAwait(false);
        if (pa9.Species > 0 && pa9.Species != _diancie && pa9.Species != _carbink)
        {
            Log($"Detected species {(Species)pa9.Species}, which shouldn't be possible. Only 'none', 'Carbink' or 'Diancie' are expected");
            return EncounterResult.InvalidSpecies;
        }

        if (pa9.Species == _diancie && pa9.Valid && pa9.EncryptionConstant > 0)
        {
            var (stop, success) = await HandleEncounter(pa9, token, raw, true).ConfigureAwait(false);

            if (success)
            {
                Log($"Your Pokémon has been received and placed in B1S{slot + 1}. Auto-save will do the rest!");
            }

            if (stop)
                return EncounterResult.ResultFound;
        }

        return pa9.Species == _diancie
            ? EncounterResult.DiancieFound
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
        DiancieFound,
        NextSlot
    }
}
