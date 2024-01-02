namespace SysBot.Pokemon;

using PKHeX.Core;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System;
using static Base.SwitchButton;

public class EncounterBotUrsalunaSV : EncounterBotSV
{
    private readonly IDumper _dumpSetting;

    public EncounterBotUrsalunaSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg, hub)
    {
        _dumpSetting = Hub.Config.Folder;
    }

    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();

            await SetupBoxState(_dumpSetting, token);

            Log("Start battle with Ursaluna");
            var later = DateTime.Now.AddSeconds(60 + 10);
            Log($"Press A till [{later}]", false);
            while (DateTime.Now <= later)
                await Click(A, 200, token);

            PK9? b1s1 = null;

            while (b1s1 == null || (Species)b1s1.Species == Species.None)
            {
                (b1s1, var bytes) = await ReadRawBoxPokemon(0, 0, token).ConfigureAwait(false);

                if (b1s1 is { Valid: true, EncryptionConstant: > 0 } && (Species)b1s1.Species != Species.None)
                {
                    var (stop, success) = await HandleEncounter(b1s1, token, bytes, true).ConfigureAwait(false);

                    if (success)
                        Log("You're Pokémon has been catched and placed in B1S1. Be sure to save your game!");

                    if (stop)
                        return;
                }

                await Click(A, 200, token).ConfigureAwait(false);
            }

            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            Log($"Single encounter duration: [{sw.Elapsed}]", false);
        }
    }
}
