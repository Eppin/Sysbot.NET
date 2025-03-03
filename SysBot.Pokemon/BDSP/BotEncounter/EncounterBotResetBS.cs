using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using PKHeX.Core.Searching;
using static SysBot.Base.SwitchButton;

namespace SysBot.Pokemon;

public class EncounterBotResetBS(PokeBotState cfg, PokeTradeHub<PB8> hub) : EncounterBotBS(cfg, hub)
{
    protected override async Task EncounterLoop(SAV8BS sav, CancellationToken token)
    {
        var pbOriginal = new PB8();

        while (!token.IsCancellationRequested)
        {
            PB8? pb8;

            Log("Looking for a Pokémon...");
            do
            {
                await Click(A, 0_050, token).ConfigureAwait(false);
                pb8 = await ReadUntilPresentPointer(Offsets.WildEncounterPointer, 0_050, 0_050, BasePokeDataOffsetsBS.BoxFormatSlotSize, token).ConfigureAwait(false); // 0x168
            } while (pb8 is null || SearchUtil.HashByDetails(pbOriginal) == SearchUtil.HashByDetails(pb8));

            var (stop, _) = await HandleEncounter(pb8, token).ConfigureAwait(false);
            if (stop)
                return;

            Log("No match, resetting the game...");
            await CloseGame(Hub.Config, token).ConfigureAwait(false);
            await StartGame(Hub.Config, token).ConfigureAwait(false);
        }
    }
}
