namespace SysBot.Pokemon;

using System.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchButton;

public class EncounterBotGimmighoulSV : EncounterBotSV
{
    private readonly IDumper _dumpSetting;

    public EncounterBotGimmighoulSV(PokeBotState cfg, PokeTradeHub<PK9> hub) : base(cfg, hub)
    {
        _dumpSetting = Hub.Config.Folder;
    }

    protected override async Task EncounterLoop(SAV9SV sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();

            await SetupBoxState(_dumpSetting, token);
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

            Log("Exit battle", false);
            PK9? b1s1 = null;
            byte[]? bytes;

            while (b1s1 == null || (Species)b1s1.Species == Species.None)
            {
                (b1s1, bytes) = await ReadRawBoxPokemon(0, 0, token).ConfigureAwait(false);

                if (b1s1 != null && b1s1.EncryptionConstant != null && (Species)b1s1.Species != Species.None)
                {
                    var (stop, success) = await HandleEncounter(b1s1, token, bytes, true).ConfigureAwait(false);

                    if (success)
                        Log("You're Pokémon has been catched and placed in B1S1. Be sure to save your game!");

                    if (stop)
                        return;
                }

                await Click(B, 200, token).ConfigureAwait(false);
            }

            Log($"Resetting game for a new {Species.Gimmighoul}");
            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
            Log($"Single encounter duration: [{sw.Elapsed}]", false);
        }
    }
}