namespace SysBot.Pokemon;

using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static Base.SwitchButton;

public class EncounterBotFloetteLZA(PokeBotState cfg, PokeTradeHub<PA9> hub) : EncounterBotLZA(cfg, hub)
{
    private readonly ushort _floette = (ushort)Species.Floette;

    protected override async Task EncounterLoop(SAV9ZA sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var dialogueCancellationTokenSource = new CancellationTokenSource();
            _ = DialogueWalking(dialogueCancellationTokenSource.Token, token).ConfigureAwait(false);

            PA9? pa9 = null;
            while (pa9 == null || pa9.Species == 0 || !pa9.Valid || pa9.EncryptionConstant == 0)
            {
                (pa9, var raw) = await ReadRawBoxPokemon(0, 0, token).ConfigureAwait(false);
                if (pa9.Species > 0 && pa9.Species != _floette)
                {
                    await dialogueCancellationTokenSource.CancelAsync();
                    Log($"Detected species {(Species)pa9.Species}, which shouldn't be possible. Only 'none' or 'Floette' are expected");
                    return;
                }

                if (pa9.Species == _floette)
                {
                    var (stop, success) = await HandleEncounter(pa9, token, raw, true).ConfigureAwait(false);

                    if (success)
                    {
                        await dialogueCancellationTokenSource.CancelAsync();
                        Log("Your Pokémon has been received and placed in B1S1. Auto-save will do the rest!");
                    }

                    if (stop)
                        return;
                }

                await Task.Delay(0_500, token);
            }

            await dialogueCancellationTokenSource.CancelAsync();
            await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
        }
    }

    private async Task DialogueWalking(CancellationToken dialogueToken, CancellationToken generalToken)
    {
        while (!dialogueToken.IsCancellationRequested && !generalToken.IsCancellationRequested)
        {
            await Click(A, 0_200, dialogueToken).ConfigureAwait(false);
        }

        Log("Stopping dialogue...");
    }
}
