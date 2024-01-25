namespace SysBot.Pokemon;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchButton;
using static Base.SwitchStick;

public class PartnerMarkBot : PokeRoutineExecutor9SV
{
    protected readonly PokeTradeHub<PK9> Hub;
    protected readonly EncounterSettingsSV Settings;
    protected readonly IDumper DumpSetting;

    public PartnerMarkBot(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg)
    {
        Hub = hub;
        Settings = Hub.Config.EncounterSV;
        DumpSetting = Hub.Config.Folder;
    }

    public override async Task MainLoop(CancellationToken token)
    {
        var settings = Hub.Config.EncounterSV;
        Log("Identifying trainer data of the host console.");
        var sav = await IdentifyTrainer(token).ConfigureAwait(false);
        await InitializeHardware(settings, token).ConfigureAwait(false);

        try
        {
            Log($"Starting {nameof(PartnerMarkBot)} loop.");
            Config.IterateNextRoutine();

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);
            await Loop(token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log(e.Message);
        }

        Log($"Ending {GetType().Name} loop.");
        await HardStop().ConfigureAwait(false);
    }

    public override async Task HardStop()
    {
        await ResetStick(CancellationToken.None).ConfigureAwait(false);
        await CleanExit(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ResetStick(CancellationToken token)
    {
        // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
        await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
    }

    private async Task Loop(CancellationToken token)
    {
        Log("Start running circles...");
        await SetStick(LEFT, -30000, 0, 0_800, token).ConfigureAwait(false);
        await Click(LSTICK, 1_000, token).ConfigureAwait(false);

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Click(A, 0_500, token);
            }
        }, token);

        while (!token.IsCancellationRequested)
        {
            var done = new[] { true, true, true, true, true, true };
            for (var i = 0; i < 6; i++)
            {
                var (pk, _) = await ReadRawPartyPokemon(i, token).ConfigureAwait(false);

                if (pk is { Species: > 0 } and { Valid: true, ChecksumValid: true })
                {
                    if (pk.IsEgg)
                    {
                        done[i] = false;
                        Log($"Party member {i + 1} is an Egg!");
                    }
                    else
                    {
                        done[i] = pk.RibbonMarkPartner;
                        var text = done[i] ? "HAS" : "doesn't have";
                        Log($"Party member {i + 1} {text} the Partner mark!");
                    }
                }
            }

            if (done.All(d => d))
            {
                Log("All party members have the Partner mark!");
                return;
            }

            var wait = TimeSpan.FromSeconds(10);
            Log($"Waiting {wait} for next party check");
            await Task.Delay((int)wait.TotalMilliseconds, token).ConfigureAwait(false);
            await Click(LSTICK, 1_000, token).ConfigureAwait(false);
        }
    }
}
